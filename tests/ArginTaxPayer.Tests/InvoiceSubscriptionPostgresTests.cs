using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class InvoiceSubscriptionPostgresTests
{
    [InvoiceSubscriptionPostgresFact]
    public async Task Concurrent_last_unit_allows_one_attempt_and_replay_does_not_charge_again()
    {
        await using var f = await Fixture.CreateAsync(2, 1);
        var operations = new[] { await f.OperationAsync(), await f.OperationAsync() };
        var results = await Task.WhenAll(operations.Select(f.StartAsync));
        Assert.Single(results, r => r.Status == Phase10DispatchAttemptStartStatus.Started);
        Assert.Single(results, r => r.BlockingCode == "subscription_exhausted");
        Assert.Equal((0L, 1L, 0L), await f.BalanceAsync());
        var started = Array.FindIndex(results, r => r.Status == Phase10DispatchAttemptStartStatus.Started);
        Assert.Equal(Phase10DispatchAttemptStartStatus.StaleOrWrongScope,
            (await f.StartAsync(operations[started])).Status);
        Assert.Equal(1L, await f.ScalarAsync("SELECT COUNT(*) FROM invoice_dispatch_attempts"));
        Assert.Equal(3L, await f.ScalarAsync("SELECT state FROM invoice_dispatch_operations WHERE id='" + operations[1-started] + "'"));
    }

    [InvoiceSubscriptionPostgresFact]
    public async Task Receipt_consumes_once_even_if_package_expired_after_send_started()
    {
        await using var f = await Fixture.CreateAsync(2, 2);
        var operation = await f.OperationAsync();
        var start = await f.StartAsync(operation);
        await f.ExecuteAsync("UPDATE customer_subscriptions SET expires_at_utc=now()-interval '1 day'");
        await using var db = f.Context();
        var evidence = new Phase10DispatchEvidenceRepository(db);
        var now = DateTime.UtcNow;
        var result = await evidence.AppendInitialReceiptAsync(operation, f.Customer, f.Taxpayer,
            start.AttemptId!.Value, start.NewStateRevision!.Value, "REF-1", new string('a',64), now);
        Assert.Equal(Phase10DispatchEvidenceStatus.Recorded, result.Status);
        Assert.Equal((1L, 0L, 1L), await f.BalanceAsync());
        var duplicate = await evidence.AppendInitialReceiptAsync(operation, f.Customer, f.Taxpayer,
            start.AttemptId.Value, start.NewStateRevision.Value, "REF-1", new string('a',64), now);
        Assert.Equal(Phase10DispatchEvidenceStatus.StaleOrWrongScope, duplicate.Status);
        Assert.Equal((1L, 0L, 1L), await f.BalanceAsync());
        Assert.Equal(1L, await f.ScalarAsync("SELECT COUNT(*) FROM subscription_credit_entries WHERE operation_type='consume'"));
    }

    [InvoiceSubscriptionPostgresFact]
    public async Task Every_timed_plan_allows_unlimited_count_but_expiry_blocks_attempts_without_writes()
    {
        foreach (var kind in new[] {1, 3, 4, 5})
        {
            await using var f = await Fixture.CreateAsync(kind, null);
            Assert.Equal(Phase10DispatchAttemptStartStatus.Started, (await f.StartAsync(await f.OperationAsync())).Status);
            Assert.Equal(Phase10DispatchAttemptStartStatus.Started, (await f.StartAsync(await f.OperationAsync())).Status);
            Assert.Equal(Phase10DispatchAttemptStartStatus.Started, (await f.StartAsync(await f.OperationAsync())).Status);
            await f.ExecuteAsync("UPDATE customer_subscriptions SET expires_at_utc=now()");
            Assert.Equal("subscription_expired", (await f.StartAsync(await f.OperationAsync())).BlockingCode);
            await using var db = f.Context();
            Assert.Equal("subscription_expired", await new InvoiceSubscriptionGate(db).CheckAsync(f.Customer));
            Assert.Equal(3L, await f.ScalarAsync("SELECT COUNT(*) FROM invoice_dispatch_attempts"));
            Assert.Equal(0L, await f.ScalarAsync("SELECT COUNT(*) FROM subscription_credit_entries"));
        }
    }

    [InvoiceSubscriptionPostgresFact]
    public async Task Expired_paid_plan_never_falls_back_to_trial_and_missing_balance_fails_closed()
    {
        await using var f = await Fixture.CreateAsync(2, 10);
        await f.ExecuteAsync("UPDATE customer_subscriptions SET expires_at_utc=now()-interval '1 second'");
        Assert.Equal("subscription_expired", (await f.StartAsync(await f.OperationAsync())).BlockingCode);
        await f.ExecuteAsync("UPDATE customer_subscriptions SET expires_at_utc=now()+interval '1 day'; DELETE FROM subscription_credit_accounts");
        Assert.Equal("subscription_exhausted", (await f.StartAsync(await f.OperationAsync())).BlockingCode);
        Assert.Equal(0L, await f.ScalarAsync("SELECT COUNT(*) FROM invoice_dispatch_attempts"));
    }

    [InvoiceSubscriptionPostgresFact]
    public async Task Concurrent_trial_claims_allow_only_two_and_no_quantity_entries()
    {
        await using var f = await Fixture.CreateAsync(null, null);
        var operations = new[] {await f.OperationAsync(), await f.OperationAsync(), await f.OperationAsync()};
        var results = await Task.WhenAll(operations.Select(f.StartAsync));
        Assert.Equal(2, results.Count(r => r.Status == Phase10DispatchAttemptStartStatus.Started));
        Assert.Single(results, r => r.BlockingCode == "subscription_required");
        Assert.Equal(0L, await f.ScalarAsync("SELECT COUNT(*) FROM subscription_credit_entries"));
    }

    [InvoiceSubscriptionPostgresFact]
    public async Task Attempt_insert_failure_rolls_back_balance_journal_and_dispatch_state()
    {
        await using var f = await Fixture.CreateAsync(2, 1);
        await f.ExecuteAsync("ALTER TABLE invoice_dispatch_attempts ADD CONSTRAINT reject_attempt CHECK(attempt_number<0)");
        var operation = await f.OperationAsync();
        await Assert.ThrowsAsync<PostgresException>(() => f.StartAsync(operation));
        Assert.Equal((1L, 0L, 0L), await f.BalanceAsync());
        Assert.Equal(0L, await f.ScalarAsync("SELECT COUNT(*) FROM subscription_credit_entries"));
        Assert.Equal(3L, await f.ScalarAsync("SELECT state FROM invoice_dispatch_operations"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string connectionString;
        private readonly string schema = "entitlement_test_" + Guid.NewGuid().ToString("N");
        public Guid Customer {get;} = Guid.NewGuid();
        public Guid Taxpayer {get;} = Guid.NewGuid();
        private Guid Subscription {get;} = Guid.NewGuid();
        private Fixture(string connection) => connectionString = connection;
        public ArginTaxPayerDbContext Context() => new(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) {SearchPath=schema}.ConnectionString).Options);

        public static async Task<Fixture> CreateAsync(int? kind, long? units)
        {
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
            if (new NpgsqlConnectionStringBuilder(connection).Database != "argintaxpayer_test")
                throw new InvalidOperationException("Only isolated test database is allowed.");
            var f = new Fixture(connection);
            await using var root = new NpgsqlConnection(connection);
            await root.OpenAsync();
            await using (var create = new NpgsqlCommand("CREATE SCHEMA " + f.schema, root)) await create.ExecuteNonQueryAsync();
            try
            {
                await f.ExecuteAsync("""
                    CREATE TABLE identity_users(id uuid PRIMARY KEY);
                    CREATE TABLE customer_subscriptions(id uuid PRIMARY KEY,customer_user_id uuid,kind int,state int,
                        activated_at_utc timestamptz,expires_at_utc timestamptz);
                    CREATE TABLE subscription_credit_accounts(subscription_id uuid PRIMARY KEY,customer_user_id uuid,
                        granted_units bigint,available_units bigint CHECK(available_units>=0),reserved_units bigint CHECK(reserved_units>=0),
                        consumed_units bigint CHECK(consumed_units>=0),revision bigint DEFAULT 1,
                        CHECK(granted_units=available_units+reserved_units+consumed_units));
                    CREATE TABLE subscription_credit_entries(id uuid PRIMARY KEY,subscription_id uuid,customer_user_id uuid,
                        operation_key text,operation_type text,reservation_key text,units bigint,recorded_at_utc timestamptz,
                        UNIQUE(subscription_id,operation_key));
                    CREATE TABLE invoice_dispatch_operations(id uuid PRIMARY KEY,customer_user_id uuid,taxpayer_id uuid,
                        state int,state_revision bigint,updated_at_utc timestamptz);
                    CREATE TABLE invoice_dispatch_attempts(id uuid PRIMARY KEY,operation_id uuid,attempt_number int,
                        endpoint_code text,started_at_utc timestamptz);
                    CREATE TABLE invoice_dispatch_receipts(id uuid PRIMARY KEY,operation_id uuid,attempt_id uuid,
                        receipt_kind int,authority_reference text,evidence_sha256 text,observed_at_utc timestamptz);
                    """);
                await using var db = f.Context();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO identity_users VALUES({f.Customer})");
                if (kind is not null)
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO customer_subscriptions VALUES({f.Subscription},{f.Customer},{kind.Value},2,
                            {DateTime.UtcNow.AddDays(-1)},{DateTime.UtcNow.AddDays(1)})
                        """);
                if (units is not null)
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO subscription_credit_accounts(subscription_id,customer_user_id,granted_units,
                            available_units,reserved_units,consumed_units)
                        VALUES({f.Subscription},{f.Customer},{units.Value},{units.Value},0,0)
                        """);
                return f;
            }
            catch {await f.DisposeAsync(); throw;}
        }
        public async Task<Guid> OperationAsync()
        {
            var id=Guid.NewGuid();
            await using var db=Context();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO invoice_dispatch_operations VALUES({id},{Customer},{Taxpayer},3,3,{DateTime.UtcNow})
                """);
            return id;
        }
        public async Task<Phase10DispatchAttemptStartResult> StartAsync(Guid operation)
        {
            await using var db=Context();
            return await new Phase10DispatchAttemptRepository(db).BeginSendAsync(operation,Customer,Taxpayer,3);
        }
        public async Task ExecuteAsync(string sql) {await using var db=Context();await db.Database.ExecuteSqlRawAsync(sql);}
        public async Task<long> ScalarAsync(string sql)
        {
            await using var db=Context();await db.Database.OpenConnectionAsync();
            await using var cmd=db.Database.GetDbConnection().CreateCommand();cmd.CommandText=sql;
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        public async Task<(long,long,long)> BalanceAsync() => (
            await ScalarAsync("SELECT available_units FROM subscription_credit_accounts"),
            await ScalarAsync("SELECT reserved_units FROM subscription_credit_accounts"),
            await ScalarAsync("SELECT consumed_units FROM subscription_credit_accounts"));
        public async ValueTask DisposeAsync()
        {
            await using var root=new NpgsqlConnection(connectionString);await root.OpenAsync();
            await using var drop=new NpgsqlCommand("DROP SCHEMA IF EXISTS " + schema + " CASCADE",root);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

internal sealed class InvoiceSubscriptionPostgresFactAttribute : FactAttribute
{
    public InvoiceSubscriptionPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1")
            Skip = "Requires explicit isolated PostgreSQL test connection.";
    }
}
