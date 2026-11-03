using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Opt-in, rollback-only fixtures; never changes existing users or commercial ownership.</summary>
public sealed class CommercialSalesPagingPostgresIntegrationTests
{
    [Fact]
    public async Task PendingAndSalesPageInPostgresWithScopedTotalsAcrossAllPages()
    {
        if (!Enabled) return;
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.Service;
        var mine = new List<CommercialPendingCashService.PendingRow>();
        var sales = new List<CommercialPendingCashService.PendingRow>();
        for (var page = 1; page <= 4; page++)
        {
            var pending = await service.ReadMineAsync(fixture.RootToken, page);
            Assert.Equal(24, pending.TotalCount);
            Assert.Equal(page, pending.Page);
            Assert.Equal(10, pending.PageSize);
            Assert.Equal(page < 3 ? 10 : page == 3 ? 4 : 0, pending.Items.Length);
            Assert.Equal(24_000L, pending.TotalGrossIrr);
            Assert.Equal(2_400L, pending.TotalCollectedIrr);
            Assert.Equal(21_600L, pending.TotalOutstandingIrr);
            Assert.All(pending.Items, row => Assert.Empty(row.Collections));
            mine.AddRange(pending.Items);

            var report = await service.ReadSalesAsync(fixture.RootToken, "month", fixture.Now, page);
            Assert.Equal(23, report.TotalCount);
            Assert.Equal(page, report.Page);
            Assert.Equal(10, report.PageSize);
            Assert.Equal(page < 3 ? 10 : page == 3 ? 3 : 0, report.Items.Length);
            Assert.Equal(23_000L, report.TotalGrossIrr);
            Assert.Equal(2_300L, report.TotalCollectedIrr);
            Assert.Equal(20_700L, report.TotalOutstandingIrr);
            sales.AddRange(report.Items);
        }
        Assert.Equal(fixture.ExpectedOrderIds, mine.Select(row => row.OrderId));
        Assert.Equal(fixture.ExpectedOrderIds.Take(23), sales.Select(row => row.OrderId));
        Assert.Equal(24, mine.Select(row => row.OrderId).Distinct().Count());
        Assert.Contains(sales, row => row.OrderId == fixture.TransferredOrder);
        Assert.DoesNotContain(sales, row => row.OrderId == fixture.OldOrder);
        Assert.DoesNotContain(mine, row => row.OrderId == fixture.ForeignOrder);
        Assert.DoesNotContain(mine, row => row.OrderId == fixture.CancelledOrder);
        var partner = await service.ReadSalesAsync(fixture.PartnerToken, "month", fixture.Now, 3);
        Assert.Equal(22, partner.TotalCount);
        Assert.Equal(2, partner.Items.Length);
        Assert.DoesNotContain(partner.Items, row => row.OrderId == fixture.TransferredOrder);
        var year = await service.ReadSalesAsync(fixture.RootToken, "year", fixture.Now, 3);
        Assert.Equal(24, year.TotalCount);
        Assert.Contains(year.Items, row => row.OrderId == fixture.OldOrder);
        Assert.Contains(fixture.Commands.Reads, sql => sql.Contains("LIMIT 10 OFFSET"));
        Assert.DoesNotContain(fixture.Commands.Reads, sql => sql.Contains("FROM commercial_collections"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadMineAsync(null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadSalesAsync(fixture.CustomerToken, "month", fixture.Now));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ReadMineAsync(fixture.RootToken, 0));
    }

    [Fact]
    public async Task SalesPartnerFilterScopesRowsTotalsAndOptionsBeforePaging()
    {
        if(!Enabled)return;
        await using var fixture=await Fixture.CreateAsync();
        var report=await fixture.Service.ReadSalesAsync(fixture.RootToken,"month",fixture.Now,
            partnerUserId:fixture.PartnerId);
        Assert.Equal(23,report.TotalCount);
        Assert.Equal(23000,report.TotalGrossIrr);
        Assert.Equal(2300,report.TotalCollectedIrr);
        Assert.Equal(20700,report.TotalOutstandingIrr);
        Assert.Equal(10,report.Items.Length);
        Assert.Contains(report.Partners,x=>x.PartnerUserId==fixture.PartnerId);
        var none=await fixture.Service.ReadSalesAsync(fixture.RootToken,"month",fixture.Now,
            partnerUserId:Guid.NewGuid());
        Assert.Empty(none.Items);
        Assert.Equal(0,none.TotalGrossIrr);
        Assert.Equal(0,none.TotalOutstandingIrr);
        Assert.Contains(none.Partners,x=>x.PartnerUserId==fixture.PartnerId);
        var scoped=await fixture.Service.ReadSalesAsync(fixture.PartnerToken,"month",fixture.Now,
            partnerUserId:Guid.NewGuid());
        Assert.Empty(scoped.Items);
        Assert.Equal(0,scoped.TotalCount);
    }

    [Fact]
    public async Task ReceiptHistoryPagesIndependentlyAndAuthorizesBeforeReadingReceipts()
    {
        if (!Enabled) return;
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.Service;
        var actual = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await service.ReadCollectionsAsync(fixture.PartnerToken, fixture.ReceiptOrder, page);
            Assert.Equal(23, result.TotalCount);
            Assert.Equal(page, result.Page);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(page < 3 ? 10 : page == 3 ? 3 : 0, result.Items.Length);
            Assert.All(result.Items, row =>
            {
                Assert.Equal(fixture.ReceiptOrder, row.OrderId);
                Assert.Equal(10L, row.AmountIrr);
                Assert.StartsWith("paging-", row.ReceiptReference);
            });
            actual.AddRange(result.Items.Select(row => row.CollectionId));
        }
        Assert.Equal(fixture.ReceiptIds, actual);
        Assert.Equal(23, actual.Distinct().Count());
        Assert.Contains(fixture.Commands.Reads, sql => sql.Contains("FROM commercial_collections") && sql.Contains("LIMIT 10 OFFSET"));
        var transferred = await service.ReadCollectionsAsync(fixture.RootToken, fixture.TransferredOrder);
        Assert.Equal(0, transferred.TotalCount);
        Assert.Empty(transferred.Items);
        foreach (var denied in new (string? Token, Guid Order)[]
        {
            (fixture.RootToken, fixture.ForeignOrder),
            (fixture.PartnerToken, fixture.TransferredOrder),
            (fixture.CustomerToken, fixture.ReceiptOrder),
            (null, fixture.ReceiptOrder),
            (fixture.RootToken, Guid.NewGuid())
        })
        {
            fixture.Commands.Reads.Clear();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.ReadCollectionsAsync(denied.Token, denied.Order));
            Assert.DoesNotContain(fixture.Commands.Reads, sql => sql.Contains("FROM commercial_collections"));
        }
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ReadCollectionsAsync(fixture.RootToken, fixture.ReceiptOrder, int.MaxValue));
    }

    private static bool Enabled => Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") == "1";

    private sealed class Commands : DbCommandInterceptor
    {
        public List<string> Reads { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Reads.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Fixture(ArginTaxPayerDbContext db, IDbContextTransaction transaction, Commands commands) : IAsyncDisposable
    {
        public DateTime Now { get; } = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        public Commands Commands { get; } = commands;
        public CommercialPendingCashService Service { get; } = new(db, new IdentityAuthorizationService(db, new IdentitySessionService(db)));
        public string RootToken { get; private set; } = "";
        public string PartnerToken { get; private set; } = "";
        public Guid PartnerId {get;private set;}
        public string CustomerToken { get; private set; } = "";
        public List<Guid> ExpectedOrderIds { get; } = [];
        public List<Guid> ReceiptIds { get; } = [];
        public Guid ReceiptOrder => ExpectedOrderIds[0];
        public Guid TransferredOrder { get; private set; }
        public Guid OldOrder { get; private set; }
        public Guid ForeignOrder { get; private set; }
        public Guid CancelledOrder { get; private set; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
                ?? throw new InvalidOperationException("Missing explicit test database connection.");
            if (new NpgsqlConnectionStringBuilder(connection).Database != "argintaxpayer_test")
                throw new InvalidOperationException("Commercial paging integration requires argintaxpayer_test.");
            var commands = new Commands();
            var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql(connection).AddInterceptors(commands).Options);
            var transaction = await db.Database.BeginTransactionAsync();
            var fixture = new Fixture(db, transaction, commands);
            try { await fixture.SeedAsync(db); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task SeedAsync(ArginTaxPayerDbContext db)
        {
            var root = Guid.NewGuid();
            var partner = Guid.NewGuid();
            PartnerId=partner;
            var outside = Guid.NewGuid();
            var customer = Guid.NewGuid();
            var transferredCustomer = Guid.NewGuid();
            RootToken = AddIdentity(db, root, IdentityRoleNames.Level1);
            PartnerToken = AddIdentity(db, partner, IdentityRoleNames.Level2);
            AddIdentity(db, outside, IdentityRoleNames.Level1);
            CustomerToken = AddIdentity(db, customer, IdentityRoleNames.Customer);
            AddIdentity(db, transferredCustomer, IdentityRoleNames.Customer);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO customer_commercial_owners(customer_user_id,root_level1_user_id,
                    partner_level2_user_id,acquisition_channel,revision,created_at_utc,updated_at_utc)
                VALUES ({transferredCustomer},{root},{partner},1,1,{Now},{Now})
                """);
            var plan = Guid.NewGuid();
            var price = Guid.NewGuid();
            var code = "paging-" + Guid.NewGuid().ToString("N");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO subscription_plan_catalog(id,code,kind,validity_days,invoice_unit_limit,is_active,created_at_utc)
                VALUES ({plan},{code},1,365,NULL,true,{Now})
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO subscription_price_versions(id,plan_id,root_admin_user_id,partner_user_id,
                    audience,price_irr,effective_from_utc,created_at_utc)
                VALUES ({price},{plan},{root},{partner},1,1000,{Now.AddYears(-1)},{Now})
                """);
            for (var index = 0; index < 22; index++)
                ExpectedOrderIds.Add(await OrderAsync(customer, root, partner, Now.AddMinutes(-index), 1));
            TransferredOrder = await OrderAsync(transferredCustomer, outside, null, Now.AddHours(-1), 1);
            ExpectedOrderIds.Add(TransferredOrder);
            OldOrder = await OrderAsync(customer, root, partner, Now.AddMonths(-1), 1);
            ExpectedOrderIds.Add(OldOrder);
            ForeignOrder = await OrderAsync(customer, outside, null, Now.AddSeconds(-1), 1);
            CancelledOrder = await OrderAsync(customer, root, partner, Now.AddSeconds(-2), 4);
            for (var index = 0; index < 23; index++)
            {
                var id = Guid.NewGuid();
                ReceiptIds.Add(id);
                var reference = "paging-" + id.ToString("N");
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO commercial_collections(id,order_id,customer_user_id,channel,amount_irr,
                        external_reference,verified_at_utc,verified_by_user_id,verifier_reference)
                    VALUES ({id},{ReceiptOrder},{customer},1,10,{reference},{Now.AddMinutes(-index)},{root},'paging-test')
                    """);
            }
            Commands.Reads.Clear();
            return;

            async Task<Guid> OrderAsync(Guid buyer, Guid owner, Guid? attributedPartner, DateTime issued, int status)
            {
                var id = Guid.NewGuid();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO commercial_orders(id,customer_user_id,plan_id,price_version_id,root_admin_user_id,
                        partner_user_id,idempotency_key,gross_amount_irr,collected_amount_irr,status,issued_at_utc)
                    VALUES ({id},{buyer},{plan},{price},{owner},{attributedPartner},{id.ToString("N")},1000,100,{status},{issued})
                    """);
                return id;
            }
        }

        private static string AddIdentity(ArginTaxPayerDbContext db, Guid id, string role)
        {
            var now = DateTime.UtcNow;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))[..43];
            db.IdentityUsers.Add(new IdentityUser
            {
                Id = id, NormalizedEmail = "paging-" + id.ToString("N") + "@example.invalid", CreatedAtUtc = now,
                Roles = [new IdentityUserRole { Role = role, AssignedAtUtc = now }]
            });
            if (role != IdentityRoleNames.Customer)
                db.IdentityMfaStates.Add(new IdentityMfaState { UserId = id, EncryptedSecret = "fixture-only", EnabledAtUtc = now });
            db.IdentitySessions.Add(new IdentitySession
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
            await transaction.DisposeAsync();
            await db.DisposeAsync();
        }
    }
}
