using System.Data;
using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Opt-in tests against the dedicated PostgreSQL database. Every fixture, including
/// actual batch registration and immutable financial evidence, is rolled back.</summary>
public sealed class PartnerBatchSettlementIntegrationTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") == "1";

    [Fact]
    public async Task Missing_terms_block_registration_and_gateway_receipts_stay_unclaimed()
    {
        if (!Enabled) return;
        await using var f = await Fixture.CreateAsync();
        var cash = await f.ReceiptAsync(1_000);
        await f.ReceiptAsync(300, channel: 3);
        var preview = await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId);
        Assert.Equal(1, preview.TotalCount);
        Assert.Equal(cash, Assert.Single(preview.Items).CollectionId);
        Assert.Null(preview.Items[0].PartnerBasisPoints);
        Assert.Equal(1, preview.MissingRateCount);
        Assert.Equal(1, preview.PendingGatewayCount);
        Assert.False(preview.CanRegister);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.RegisterAsync(preview.PreviewFingerprint));
        Assert.Empty((await f.Service.GetBatchesAsync(f.RootToken, f.PartnerId)).Items);
        await f.Service.SaveTermsAsync(f.RootToken, f.PartnerId, 2_500);
        var configured = await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId);
        Assert.True(configured.CanRegister);
        Assert.Equal(0, configured.MissingRateCount);
        Assert.Equal(1_000L, configured.TotalCollectedIrr);
        Assert.Equal(250L, configured.TotalPartnerShareIrr);
        Assert.Equal(750L, configured.NetToRootIrr);
    }

    [Fact]
    public async Task New_receipts_on_old_unactivated_order_are_recognized_once_and_late_costs_are_only_deltas()
    {
        if (!Enabled) return;
        await using var f = await Fixture.CreateAsync();
        await f.Service.SaveTermsAsync(f.RootToken, f.PartnerId, 2_500);
        var receipt = await f.ReceiptAsync(1_000);
        var first = await f.RegisterCurrentAsync();
        Assert.Equal(1_000L, first.TotalCollectedIrr);
        Assert.Equal(750L, first.NetToRootIrr);
        Assert.Empty((await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId)).Items);

        var rootExpense = new PartnerBatchExpenseRequest(f.PartnerId, receipt, 100, true,
            "root-paid", "bank charge", Guid.NewGuid());
        var expenseId = await f.Service.AddExpenseAsync(f.RootToken, rootExpense);
        Assert.Equal(expenseId, await f.Service.AddExpenseAsync(f.RootToken, rootExpense));
        var second = await f.RegisterCurrentAsync();
        Assert.Equal(0L, second.TotalCollectedIrr);
        Assert.Equal(100L, second.TotalCostIrr);
        Assert.Equal(100L, second.RootPaidCostIrr);
        Assert.Equal(-25L, second.TotalPartnerShareIrr);
        Assert.Equal(-75L, second.TotalRootShareIrr);
        Assert.Equal(25L, second.NetToRootIrr);

        await f.Service.AddExpenseAsync(f.RootToken, new PartnerBatchExpenseRequest(f.PartnerId, receipt,
            200, false, "partner-paid", "partner operating expense", Guid.NewGuid()));
        var correction = await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId);
        var line = Assert.Single(correction.Items);
        Assert.Equal(1_000L, line.GrossIrr);
        Assert.Equal(300L, line.ApprovedCostIrr);
        Assert.Equal(100L, line.RootPaidCostIrr);
        Assert.Equal(775L, line.PreviousNetToRootIrr);
        Assert.Equal(625L, line.NetToRootIrr);
        Assert.Equal(-150L, line.DeltaNetToRootIrr);
        var third = await f.RegisterAsync(correction.PreviewFingerprint);
        Assert.Equal(-150L, third.NetToRootIrr);
        Assert.Equal(0L, third.TotalCollectedIrr);
        Assert.Equal(200L, third.TotalCostIrr);
        Assert.Empty((await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId)).Items);

        var newReceipt = await f.ReceiptAsync(400, channel: 2);
        var fourth = await f.RegisterCurrentAsync();
        Assert.Equal(400L, fourth.TotalCollectedIrr);
        Assert.Equal(0L, fourth.CashIrr);
        Assert.Equal(400L, fourth.BankIrr);
        Assert.Equal(300L, fourth.NetToRootIrr);
        var saved = await f.Service.GetBatchAsync(f.RootToken, fourth.BatchId);
        Assert.Equal(newReceipt, Assert.Single(saved.Items).CollectionId);
        Assert.Equal(f.OrderId, saved.Items[0].OrderId);
        Assert.Equal(925L, first.NetToRootIrr + second.NetToRootIrr + third.NetToRootIrr + fourth.NetToRootIrr);
    }

    [Fact]
    public async Task Stale_preview_and_conflicting_idempotency_leave_no_partial_batches_or_duplicate_lines()
    {
        if (!Enabled) return;
        await using var f = await Fixture.CreateAsync();
        await f.Service.SaveTermsAsync(f.RootToken, f.PartnerId, 2_500);
        var receipt = await f.ReceiptAsync(1_000);
        var stale = await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId);
        await f.Service.AddExpenseAsync(f.RootToken, new PartnerBatchExpenseRequest(f.PartnerId, receipt,
            100, false, "late", "changes preview", Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.RegisterAsync(stale.PreviewFingerprint));
        Assert.Empty((await f.Service.GetBatchesAsync(f.RootToken, f.PartnerId)).Items);
        var current = await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId);
        var request = new PartnerBatchRegisterRequest(f.PartnerId, current.PreviewFingerprint,
            Guid.NewGuid(), "confirmed transfer", "idempotency fixture");
        var first = await f.Service.RegisterAsync(f.RootToken, request);
        var replay = await f.Service.RegisterAsync(f.RootToken, request);
        Assert.Equal(first, replay);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RegisterAsync(f.RootToken,
            request with { Note = "different exact payload" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RegisterAsync(f.RootToken,
            request with { OperationKey = Guid.NewGuid() }));
        var batches = await f.Service.GetBatchesAsync(f.RootToken, f.PartnerId);
        Assert.Equal(1, batches.TotalCount);
        var detail = await f.Service.GetBatchAsync(f.RootToken, first.BatchId, print: true);
        Assert.Equal(receipt, Assert.Single(detail.Items).CollectionId);
        Assert.Equal(900L, detail.Items[0].RootShareIrr + detail.Items[0].PartnerShareIrr);
        Assert.Empty((await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId)).Items);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.AddExpenseAsync(f.RootToken,
            new PartnerBatchExpenseRequest(f.PartnerId, receipt, 901, true, "too much", "over receipt", Guid.NewGuid())));
        Assert.Empty((await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId)).Items);
    }

    [Fact]
    public async Task Saved_voucher_and_rate_remain_frozen_after_profile_terms_and_ownership_changes()
    {
        if (!Enabled) return;
        await using var f = await Fixture.CreateAsync();
        await f.Service.SaveTermsAsync(f.RootToken, f.PartnerId, 2_500);
        var receipt = await f.ReceiptAsync(1_000);
        var batch = await f.RegisterCurrentAsync();
        var before = await f.Service.GetBatchAsync(f.RootToken, batch.BatchId, print: true);
        var original = Assert.Single(before.Items);
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE identity_users SET normalized_email={"changed-" + f.CustomerId.ToString("N") + "@example.invalid"}
            WHERE id={f.CustomerId};
            UPDATE subscription_plan_catalog SET code={"changed-" + Guid.NewGuid().ToString("N")} WHERE id={f.PlanId};
            UPDATE customer_commercial_owners SET partner_level2_user_id={f.OtherPartnerId},revision=revision+1
            WHERE customer_user_id={f.CustomerId};
            """);
        await f.Service.SaveTermsAsync(f.RootToken, f.PartnerId, 9_000);
        var after = await f.Service.GetBatchAsync(f.PartnerToken, batch.BatchId, print: true);
        Assert.Equal(before.Batch, after.Batch);
        Assert.Equal(original, Assert.Single(after.Items));
        await f.Service.AddExpenseAsync(f.RootToken, new PartnerBatchExpenseRequest(f.PartnerId, receipt,
            100, false, "after transfer", "frozen owner", Guid.NewGuid()));
        var frozen = await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId);
        Assert.Equal(2_500, Assert.Single(frozen.Items).PartnerBasisPoints);
        Assert.Equal(-75L, frozen.NetToRootIrr);
        var nextReceipt = await f.ReceiptAsync(200);
        var transferred = await f.Service.GetPreviewAsync(f.RootToken, f.OtherPartnerId);
        Assert.Equal(nextReceipt, Assert.Single(transferred.Items).CollectionId);
        Assert.Equal(1, transferred.MissingRateCount);
        Assert.DoesNotContain(transferred.Items, item => item.CollectionId == receipt);

        // Level2 retains its immutable vouchers when the current customer root changes.
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE customer_commercial_owners SET root_level1_user_id={f.OtherRootId},
                partner_level2_user_id={f.PartnerId},revision=revision+1 WHERE customer_user_id={f.CustomerId}
            """);
        await f.Service.SaveTermsAsync(f.OtherRootToken, f.PartnerId, 3_000);
        var newRootPreview = await f.Service.GetPreviewAsync(f.PartnerToken, f.PartnerId);
        Assert.Equal(nextReceipt, Assert.Single(newRootPreview.Items).CollectionId);
        var newRootBatch = await f.Service.RegisterAsync(f.OtherRootToken,
            new PartnerBatchRegisterRequest(f.PartnerId, newRootPreview.PreviewFingerprint,
                Guid.NewGuid(), "new root transfer", "new current scope"));
        Assert.Equal(140L, newRootBatch.NetToRootIrr);
        var partnerHistory = await f.Service.GetBatchesAsync(f.PartnerToken, f.PartnerId);
        Assert.Equal(2, partnerHistory.TotalCount);
        Assert.Contains(partnerHistory.Items, item => item.BatchId == batch.BatchId);
        Assert.Contains(partnerHistory.Items, item => item.BatchId == newRootBatch.BatchId);
        Assert.Equal(1, (await f.Service.GetBatchesAsync(f.RootToken, f.PartnerId)).TotalCount);
        Assert.Equal(1, (await f.Service.GetBatchesAsync(f.OtherRootToken, f.PartnerId)).TotalCount);
        Assert.Equal(before.Batch, (await f.Service.GetBatchAsync(f.PartnerToken, batch.BatchId)).Batch);
    }

    [Fact]
    public async Task Effective_legacy_terms_and_costs_are_respected_while_legacy_settled_receipts_are_excluded()
    {
        if (!Enabled) return;
        await using var f = await Fixture.CreateAsync();
        var eligible = await f.ReceiptAsync(1_000);
        var legacy = await f.ReceiptAsync(2_000);
        var term = Guid.NewGuid();
        var allocation = Guid.NewGuid();
        var unclaimedAllocation = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO partner_share_terms(id,root_user_id,partner_user_id,partner_basis_points,
                effective_from_utc,approved_by_level1_user_id)
            VALUES ({term},{f.RootId},{f.PartnerId},2500,{now.AddDays(-1)},{f.RootId});
            INSERT INTO commercial_approved_costs(id,collection_id,amount_irr,explanation,
                approved_by_level1_user_id,approved_at_utc)
            VALUES ({Guid.NewGuid()},{eligible},100,'legacy root-paid cost',{f.RootId},{now});
            INSERT INTO commercial_revenue_allocations(id,collection_id,root_user_id,partner_user_id,
                share_term_id,gross_collected_irr,approved_cost_irr,partner_share_irr,root_share_irr,posted_at_utc)
            VALUES ({allocation},{legacy},{f.RootId},{f.PartnerId},{term},2000,0,500,1500,{now}),
                ({unclaimedAllocation},{eligible},{f.RootId},{f.PartnerId},{term},1000,100,225,675,{now});
            INSERT INTO partner_settlement_events(id,allocation_id,root_user_id,partner_user_id,kind,amount_irr,
                external_reference,recorded_by_level1_user_id,reason,recorded_at_utc)
            VALUES ({Guid.NewGuid()},{allocation},{f.RootId},{f.PartnerId},1,500,
                {"legacy-" + allocation.ToString("N")},{f.RootId},'existing evidence',{now});
            """);
        var preview = await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId);
        Assert.Equal(1, preview.LegacyConflictCount);
        Assert.Equal(0, preview.MissingRateCount);
        var line = Assert.Single(preview.Items);
        Assert.Equal(eligible, line.CollectionId);
        Assert.Equal(2_500, line.PartnerBasisPoints);
        Assert.Equal(100L, line.RootPaidCostIrr);
        Assert.Equal(775L, line.NetToRootIrr);
        var batch = await f.RegisterCurrentAsync();
        Assert.Equal(1_000L, batch.TotalCollectedIrr);
        Assert.Equal(775L, batch.NetToRootIrr);
        Assert.Equal(1, (await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId)).LegacyConflictCount);
        var oldService = new PartnerSettlementService(f.Db,
            new IdentityAuthorizationService(f.Db, new IdentitySessionService(f.Db)));
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => oldService.RecordPaymentAsync(f.RootToken,
            unclaimedAllocation, 225, "must-not-pay-again", "already covered by batch"));
        Assert.Contains("already covered", rejected.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, (await f.Service.GetBatchesAsync(f.RootToken, f.PartnerId)).TotalCount);
        Assert.Equal(1, (await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId)).LegacyConflictCount);
    }

    [Fact]
    public async Task Reads_and_writes_enforce_root_scope_partner_ownership_and_current_MFA()
    {
        if (!Enabled) return;
        await using var f = await Fixture.CreateAsync();
        await f.Service.SaveTermsAsync(f.RootToken, f.PartnerId, 2_500);
        var receipt = await f.ReceiptAsync(1_000);
        var preview = await f.Service.GetPreviewAsync(f.PartnerToken, f.PartnerId);
        Assert.Equal(1_000L, preview.TotalCollectedIrr);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveTermsAsync(f.PartnerToken, f.PartnerId, 100));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.RegisterAsync(f.PartnerToken,
            new PartnerBatchRegisterRequest(f.PartnerId, preview.PreviewFingerprint, Guid.NewGuid(), "no", "no")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.AddExpenseAsync(f.PartnerToken,
            new PartnerBatchExpenseRequest(f.PartnerId, receipt, 10, false, "no", "no", Guid.NewGuid())));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetPreviewAsync(f.OtherRootToken, f.PartnerId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetPreviewAsync(f.OtherPartnerToken, f.PartnerId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetPreviewAsync(f.CustomerToken, f.PartnerId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetPreviewAsync(null, f.PartnerId));
        var batch = await f.RegisterCurrentAsync();
        Assert.Equal(batch.BatchId, (await f.Service.GetBatchAsync(f.PartnerToken, batch.BatchId)).Batch.BatchId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetBatchAsync(f.OtherRootToken, batch.BatchId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetBatchAsync(f.OtherPartnerToken, batch.BatchId, print: true));
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity_mfa_states SET enabled_at_utc=NULL WHERE user_id={f.RootId}");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveTermsAsync(f.RootToken, f.PartnerId, 100));
    }

    [Fact]
    public async Task Preview_detail_and_history_use_ten_database_selected_rows_and_print_returns_all_saved_lines()
    {
        if (!Enabled) return;
        await using var f = await Fixture.CreateAsync();
        await f.Service.SaveTermsAsync(f.RootToken, f.PartnerId, 2_500);
        var receipts = new HashSet<Guid>();
        for (var i = 0; i < 23; i++) receipts.Add(await f.ReceiptAsync(100));
        var actual = new List<Guid>();
        f.Commands.Reads.Clear();
        for (var page = 1; page <= 4; page++)
        {
            var preview = await f.Service.GetPreviewAsync(f.RootToken, f.PartnerId, page);
            Assert.Equal(23, preview.TotalCount);
            Assert.Equal(10, preview.PageSize);
            Assert.Equal(page, preview.Page);
            Assert.Equal(page < 3 ? 10 : page == 3 ? 3 : 0, preview.Items.Length);
            Assert.Equal(2_300L, preview.TotalCollectedIrr);
            Assert.Equal(1_725L, preview.NetToRootIrr);
            actual.AddRange(preview.Items.Select(row => row.CollectionId));
        }
        Assert.Equal(23, actual.Distinct().Count());
        Assert.True(receipts.SetEquals(actual));
        Assert.Contains(f.Commands.Reads, sql => sql.Contains("SELECT line::text FROM lines ORDER BY id LIMIT", StringComparison.OrdinalIgnoreCase));
        var batch = await f.RegisterCurrentAsync();
        for (var page = 1; page <= 3; page++)
        {
            var detail = await f.Service.GetBatchAsync(f.PartnerToken, batch.BatchId, page);
            Assert.Equal(23, detail.TotalCount);
            Assert.Equal(10, detail.PageSize);
            Assert.Equal(page < 3 ? 10 : 3, detail.Items.Length);
        }
        var print = await f.Service.GetBatchAsync(f.RootToken, batch.BatchId, print: true);
        Assert.Equal(23, print.Items.Length);
        Assert.True(receipts.SetEquals(print.Items.Select(row => row.CollectionId)));
        for (var i = 0; i < 10; i++)
        {
            await f.ReceiptAsync(100);
            await f.RegisterCurrentAsync();
        }
        var history1 = await f.Service.GetBatchesAsync(f.RootToken, f.PartnerId);
        var history2 = await f.Service.GetBatchesAsync(f.PartnerToken, f.PartnerId, 2);
        Assert.Equal(11, history1.TotalCount);
        Assert.Equal(10, history1.Items.Length);
        Assert.Single(history2.Items);
        Assert.Equal(11, history2.TotalCount);
        Assert.Empty(history1.Items.Select(x => x.BatchId).Intersect(history2.Items.Select(x => x.BatchId)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Service.GetPreviewAsync(f.RootToken, f.PartnerId, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Service.GetBatchesAsync(f.RootToken, f.PartnerId, int.MaxValue));
    }

    // Capture provider command execution too: the service intentionally uses DbCommand
    // for its receipt CTEs, so EF interceptors alone cannot verify database pagination.
    private sealed class Commands : ILoggerProvider
    {
        public List<string> Reads { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CommandLogger(this);
        public void Dispose() { }
        private sealed class CommandLogger(Commands owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel)) owner.Reads.Add(formatter(state, exception));
            }
        }
    }

    private sealed class Fixture(ArginTaxPayerDbContext db, IDbContextTransaction transaction, Commands commands,
        NpgsqlDataSource dataSource, ILoggerFactory loggerFactory) : IAsyncDisposable
    {
        public ArginTaxPayerDbContext Db { get; } = db;
        public Commands Commands { get; } = commands;
        public PartnerBatchSettlementService Service { get; } = new(db, new IdentityAuthorizationService(db, new IdentitySessionService(db)));
        public Guid RootId { get; } = Guid.NewGuid();
        public Guid PartnerId { get; } = Guid.NewGuid();
        public Guid OtherPartnerId { get; } = Guid.NewGuid();
        public Guid OtherRootId { get; } = Guid.NewGuid();
        public Guid CustomerId { get; } = Guid.NewGuid();
        public Guid PlanId { get; } = Guid.NewGuid();
        public Guid OrderId { get; } = Guid.NewGuid();
        public string RootToken { get; private set; } = "";
        public string PartnerToken { get; private set; } = "";
        public string OtherRootToken { get; private set; } = "";
        public string OtherPartnerToken { get; private set; } = "";
        public string CustomerToken { get; private set; } = "";

        public static async Task<Fixture> CreateAsync()
        {
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
                ?? throw new InvalidOperationException("Missing explicit test database connection.");
            if (new NpgsqlConnectionStringBuilder(connection).Database != "argintaxpayer_test")
                throw new InvalidOperationException("Partner batch integration requires argintaxpayer_test.");
            var commands = new Commands();
            var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(commands));
            var dataSource = new NpgsqlDataSourceBuilder(connection).UseLoggerFactory(loggerFactory).Build();
            var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql(dataSource).Options);
            var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var fixture = new Fixture(db, tx, commands, dataSource, loggerFactory);
            try { await fixture.SeedAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task SeedAsync()
        {
            RootToken = AddIdentity(RootId, IdentityRoleNames.Level1);
            PartnerToken = AddIdentity(PartnerId, IdentityRoleNames.Level2);
            OtherPartnerToken = AddIdentity(OtherPartnerId, IdentityRoleNames.Level2);
            OtherRootToken = AddIdentity(OtherRootId, IdentityRoleNames.Level1);
            CustomerToken = AddIdentity(CustomerId, IdentityRoleNames.Customer);
            await Db.SaveChangesAsync();
            var now = DateTime.UtcNow;
            var price = Guid.NewGuid();
            await Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO customer_commercial_owners(customer_user_id,root_level1_user_id,partner_level2_user_id,
                    acquisition_channel,revision,created_at_utc,updated_at_utc)
                VALUES ({CustomerId},{RootId},{PartnerId},1,1,{now},{now});
                INSERT INTO subscription_plan_catalog(id,code,kind,validity_days,invoice_unit_limit,is_active,created_at_utc)
                VALUES ({PlanId},{"batch-" + PlanId.ToString("N")},1,365,NULL,true,{now});
                INSERT INTO subscription_price_versions(id,plan_id,root_admin_user_id,partner_user_id,
                    audience,price_irr,effective_from_utc,created_at_utc)
                VALUES ({price},{PlanId},{RootId},{PartnerId},1,100000,{now.AddYears(-3)},{now});
                INSERT INTO commercial_orders(id,customer_user_id,plan_id,price_version_id,root_admin_user_id,
                    partner_user_id,idempotency_key,gross_amount_irr,collected_amount_irr,status,issued_at_utc)
                VALUES ({OrderId},{CustomerId},{PlanId},{price},{RootId},{PartnerId},{OrderId.ToString("N")},100000,0,1,{now.AddYears(-2)});
                """);
        }

        public async Task<Guid> ReceiptAsync(long amount, int channel = 1)
        {
            var id = Guid.NewGuid();
            Guid? verifier = channel == 3 ? null : RootId;
            await Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial_collections(id,order_id,customer_user_id,channel,amount_irr,external_reference,
                    verified_at_utc,verified_by_user_id,verifier_reference)
                VALUES ({id},{OrderId},{CustomerId},{channel},{amount},{"batch-" + id.ToString("N")},
                    {DateTime.UtcNow},{verifier},'batch-fixture');
                UPDATE commercial_orders SET collected_amount_irr=collected_amount_irr+{amount} WHERE id={OrderId};
                """);
            return id;
        }

        public Task<PartnerBatchSummary> RegisterAsync(string fingerprint) => Service.RegisterAsync(RootToken,
            new PartnerBatchRegisterRequest(PartnerId, fingerprint, Guid.NewGuid(), "confirmed-" + Guid.NewGuid().ToString("N"), "fixture transfer"));

        public async Task<PartnerBatchSummary> RegisterCurrentAsync() =>
            await RegisterAsync((await Service.GetPreviewAsync(RootToken, PartnerId)).PreviewFingerprint);

        private string AddIdentity(Guid id, string role)
        {
            var now = DateTime.UtcNow;
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            Db.IdentityUsers.Add(new IdentityUser
            {
                Id = id, NormalizedEmail = "batch-" + id.ToString("N") + "@example.invalid", CreatedAtUtc = now,
                Roles = [new IdentityUserRole { Role = role, AssignedAtUtc = now }]
            });
            if (role != IdentityRoleNames.Customer)
                Db.IdentityMfaStates.Add(new IdentityMfaState { UserId = id, EncryptedSecret = "fixture-only", EnabledAtUtc = now });
            Db.IdentitySessions.Add(new IdentitySession
            {
                Id = Guid.NewGuid(), UserId = id, CreatedAtUtc = now,
                AccessTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant(),
                RefreshTokenHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
                AccessExpiresAtUtc = now.AddHours(1), RefreshExpiresAtUtc = now.AddDays(1)
            });
            return token;
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await Db.DisposeAsync();
            await dataSource.DisposeAsync();
            loggerFactory.Dispose();
        }
    }
}
