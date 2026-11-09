using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Customers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Actual onboarding commands in a disposable local schema; authority connection is always fake.</summary>
public sealed class Phase18FiscalMemoryOnboardingDatabaseTests
{
    [Phase18OnboardingPostgresFact]
    public async Task Explicit_classification_and_verified_activation_persist_both_environments_and_audit()
    {
        foreach (var environment in new[] { FiscalConnectionEnvironment.Production, FiscalConnectionEnvironment.Sandbox })
        {
            await using var f = await Fixture.CreateAsync();
            await using var db = f.Context();
            var connection = new FakeConnection(f.Identifier);
            var service = f.Service(db, connection);
            var classified = await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory, new(environment, 1));
            Assert.Equal(200, classified.StatusCode);
            Assert.Equal(environment, classified.Memory!.AuthorityEnvironment);
            Assert.Equal(2, classified.Memory.Revision);
            Assert.Equal(0, connection.Calls);
            var active = await service.VerifyAndActivateAsync(f.RootToken, f.Taxpayer, f.Memory, new(2));
            Assert.Equal(200, active.StatusCode);
            var stored = await db.FiscalMemories.AsNoTracking().SingleAsync();
            Assert.Equal(FiscalMemoryStatus.Active, stored.Status);
            Assert.Equal(environment, stored.AuthorityEnvironment);
            Assert.Equal(3, stored.Revision);
            Assert.Equal(1, connection.Calls);
            Assert.Equal((f.RootToken, f.Taxpayer, f.Memory), connection.LastScope);
            Assert.Equal(2, await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
            Assert.Equal((int)environment, await f.ScalarAsync("SELECT authority_environment FROM fiscal_memory_authority_events WHERE action='activate'"));
            Assert.Equal(1, await f.ScalarAsync("SELECT from_status FROM fiscal_memory_authority_events WHERE action='activate'"));
            Assert.Equal(2, await f.ScalarAsync("SELECT to_status FROM fiscal_memory_authority_events WHERE action='activate'"));
            Assert.Equal(409, (await service.VerifyAndActivateAsync(f.RootToken, f.Taxpayer, f.Memory, new(3))).StatusCode);
            Assert.Equal(1, connection.Calls);
            await Assert.ThrowsAsync<PostgresException>(() => f.ExecuteAsync("DELETE FROM fiscal_memory_authority_events"));
        }
    }

    [Phase18OnboardingPostgresFact]
    public async Task Reclassification_and_cross_taxpayer_memory_are_rejected_without_authority_io()
    {
        await using var f = await Fixture.CreateAsync();
        await using var db = f.Context();
        var connection = new FakeConnection(f.Identifier);
        var service = f.Service(db, connection);
        Assert.Equal(404, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, Guid.NewGuid(),
            new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
        Assert.Equal(200, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
            new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
        Assert.Equal(409, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
            new(FiscalConnectionEnvironment.Sandbox, 2))).StatusCode);
        Assert.Equal(1, await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
        Assert.Equal(0, connection.Calls);
    }

    [Phase18OnboardingPostgresFact]
    public async Task Customer_cannot_classify_but_can_activate_own_classified_memory()
    {
        await using var f = await Fixture.CreateAsync();
        await using var db = f.Context();
        var connection = new FakeConnection(f.Identifier);
        var service = f.Service(db, connection);
        Assert.Equal(403, (await service.ClassifyAsync(f.CustomerToken, f.Taxpayer, f.Memory,
            new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
        var unclassified = await service.VerifyAndActivateAsync(f.CustomerToken, f.Taxpayer, f.Memory, new(1));
        Assert.Equal(409, unclassified.StatusCode);
        Assert.Equal("memory_environment_unclassified", unclassified.Code);
        Assert.Equal(0, connection.Calls);
        Assert.Equal(0, await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
        Assert.Equal(200, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
            new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
        Assert.Equal(200, (await service.VerifyAndActivateAsync(f.CustomerToken, f.Taxpayer, f.Memory, new(2))).StatusCode);
        Assert.Equal(FiscalMemoryStatus.Active, (await db.FiscalMemories.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(1, connection.Calls);
        Assert.Equal((f.CustomerToken, f.Taxpayer, f.Memory), connection.LastScope);
        Assert.Equal(2, await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
    }

    [Phase18OnboardingPostgresFact]
    public async Task Administrator_without_current_mfa_cannot_classify_or_activate()
    {
        await using var f = await Fixture.CreateAsync();
        await using var db = f.Context();
        var connection = new FakeConnection(f.Identifier);
        var service = f.Service(db, connection);
        await f.ExecuteAsync("DELETE FROM identity_mfa_states");
        Assert.Equal(401, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
            new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
        Assert.Equal(401, (await service.VerifyAndActivateAsync(f.RootToken, f.Taxpayer, f.Memory, new(1))).StatusCode);
        Assert.Equal(0, connection.Calls);
        Assert.Equal(0, await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
    }

    [Phase18OnboardingPostgresFact]
    public async Task Authenticated_wrong_taxpayer_or_inactive_authority_memory_does_not_activate()
    {
        foreach (var wrongIdentifier in new[] { true, false })
        {
            await using var f = await Fixture.CreateAsync();
            await using var db = f.Context();
            var connection = new FakeConnection(wrongIdentifier ? "9999999999" : f.Identifier,
                wrongIdentifier ? "ACTIVE" : "INACTIVE");
            var service = f.Service(db, connection);
            Assert.Equal(200, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
                new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
            var result = await service.VerifyAndActivateAsync(f.RootToken, f.Taxpayer, f.Memory, new(2));
            Assert.Equal(422, result.StatusCode);
            Assert.Equal("authority_memory_inactive_or_taxpayer_mismatch", result.Code);
            Assert.Equal(FiscalMemoryStatus.PendingVerification, (await db.FiscalMemories.AsNoTracking().SingleAsync()).Status);
            Assert.Equal(2, await f.ScalarAsync("SELECT revision FROM fiscal_memories"));
            Assert.Equal(1, await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
            Assert.Equal(1, connection.Calls);
        }
    }

    [Phase18OnboardingPostgresFact]
    public async Task Audit_insert_failure_rolls_back_classification_and_activation()
    {
        foreach (var action in new[] { "classify", "activate" })
        {
            await using var f = await Fixture.CreateAsync();
            await using var db = f.Context();
            var connection = new FakeConnection(f.Identifier);
            var service = f.Service(db, connection);
            if (action == "activate")
                Assert.Equal(200, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
                    new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
            await f.ExecuteAsync($"ALTER TABLE fiscal_memory_authority_events ADD CONSTRAINT reject_test_audit CHECK(action<>'{action}')");
            if (action == "classify")
                await Assert.ThrowsAsync<PostgresException>(() => service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
                    new(FiscalConnectionEnvironment.Production, 1)));
            else
                await Assert.ThrowsAsync<PostgresException>(() => service.VerifyAndActivateAsync(
                    f.RootToken, f.Taxpayer, f.Memory, new(2)));
            var memory = await db.FiscalMemories.AsNoTracking().SingleAsync();
            Assert.Equal(FiscalMemoryStatus.PendingVerification, memory.Status);
            Assert.Equal(action == "classify" ? 1 : 2, memory.Revision);
            Assert.Equal(action == "classify" ? 0 : 1,
                await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
            if (action == "classify") Assert.Null(memory.AuthorityEnvironment);
        }
    }

    [Phase18OnboardingPostgresFact]
    public async Task Revoked_membership_or_changed_selected_key_blocks_activation()
    {
        foreach (var changeKey in new[] { true, false })
        {
            await using var f = await Fixture.CreateAsync();
            await using var db = f.Context();
            var connection = new FakeConnection(f.Identifier) { DuringTest = () => f.ExecuteAsync(changeKey
                ? "DELETE FROM signing_key_versions"
                : "UPDATE customer_taxpayer_memberships SET unlinked_at_utc=now(),unlinked_by_user_id=linked_by_user_id") };
            var service = f.Service(db, connection);
            Assert.Equal(200, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
                new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
            var result = await service.VerifyAndActivateAsync(f.RootToken, f.Taxpayer, f.Memory, new(2));
            Assert.Equal(changeKey ? 409 : 403, result.StatusCode);
            Assert.Equal(1, connection.Calls);
            Assert.Equal(FiscalMemoryStatus.PendingVerification, (await db.FiscalMemories.AsNoTracking().SingleAsync()).Status);
            Assert.Equal(1, await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
        }
    }

    [Phase18OnboardingPostgresFact]
    public async Task Key_without_public_material_blocks_activation_before_authority_io()
    {
        await using var f = await Fixture.CreateAsync();
        await using var db = f.Context();
        var connection = new FakeConnection(f.Identifier);
        var service = f.Service(db, connection);
        Assert.Equal(200, (await service.ClassifyAsync(f.RootToken, f.Taxpayer, f.Memory,
            new(FiscalConnectionEnvironment.Production, 1))).StatusCode);
        await f.ExecuteAsync("UPDATE signing_key_versions SET public_spki_der=NULL");

        var result = await service.VerifyAndActivateAsync(f.RootToken, f.Taxpayer, f.Memory, new(2));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("key_not_ready", result.Code);
        Assert.Equal(0, connection.Calls);
        Assert.Equal(FiscalMemoryStatus.PendingVerification, (await db.FiscalMemories.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(1, await f.ScalarAsync("SELECT count(*) FROM fiscal_memory_authority_events"));
    }

    private sealed class FakeConnection(string identifier, string status = "ACTIVE") : ISigningReadOnlyConnectionTest
    {
        public int Calls { get; private set; }
        public (string?, Guid, Guid) LastScope { get; private set; }
        public Func<Task>? DuringTest { get; init; }
        public async Task<SigningConnectionTestResult> TestAsync(string? bearer, Guid taxpayerId,
            Guid fiscalMemoryId, CancellationToken ct = default,
            FiscalConnectionEnvironment? oneTimeEnvironment = null)
        {
            Calls++;
            LastScope = (bearer, taxpayerId, fiscalMemoryId);
            if (DuringTest is not null) await DuringTest();
            return new(SigningConnectionTestStatus.Authenticated, DateTime.UtcNow,
                "authority_authenticated", true, FiscalInformation: new("synthetic", status, "", identifier));
        }
    }

    private sealed class Fixture(string connection, string schema) : IAsyncDisposable
    {
        private string Schema => schema;
        public Guid Root { get; } = Guid.NewGuid();
        public Guid Customer { get; } = Guid.NewGuid();
        public Guid Taxpayer { get; } = Guid.NewGuid();
        public Guid Memory { get; } = Guid.NewGuid();
        public string RootToken { get; } = Token();
        public string CustomerToken { get; } = Token();
        public string Identifier => "0013541339";
        public ArginTaxPayerDbContext Context() => new(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString).Options);
        public FiscalMemoryAuthorityOnboardingService Service(ArginTaxPayerDbContext db, ISigningReadOnlyConnectionTest test) =>
            new(db, new(db, new(db, new(db)), new ParticipantScopeService(db)), test, new InvoiceSubscriptionGate(db));

        public static async Task<Fixture> CreateAsync()
        {
            var raw = Environment.GetEnvironmentVariable("PHASE18_ONBOARDING_TEST_CONNECTION")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
                ?? throw new InvalidOperationException("Explicit test connection required.");
            var settings = new NpgsqlConnectionStringBuilder(raw);
            if (settings.Host is not ("localhost" or "127.0.0.1") || settings.Database != "argintaxpayer_test")
                throw new InvalidOperationException("Refusing any database except loopback argintaxpayer_test.");
            var f = new Fixture(raw, "p18_onboarding_" + Guid.NewGuid().ToString("N"));
            await using var root = new NpgsqlConnection(raw);
            await root.OpenAsync();
            await using var create = root.CreateCommand();
            create.CommandText = $"CREATE SCHEMA {f.Schema}";
            await create.ExecuteNonQueryAsync();
            try
            {
                foreach (var table in new[] { "identity_users", "identity_user_roles", "identity_sessions", "identity_mfa_states",
                    "taxpayer_profiles", "customer_commercial_owners", "customer_taxpayer_memberships", "fiscal_memories" })
                {
                    create.CommandText = $"CREATE TABLE {f.Schema}.{table} (LIKE public.{table} INCLUDING ALL)";
                    await create.ExecuteNonQueryAsync();
                }
                await f.ExecuteAsync("""
ALTER TABLE fiscal_memories ADD COLUMN IF NOT EXISTS authority_environment integer;
CREATE TABLE invoice_dispatch_operations(id uuid PRIMARY KEY,fiscal_memory_id uuid,customer_user_id uuid);
CREATE TABLE customer_subscriptions(id uuid PRIMARY KEY,customer_user_id uuid,kind integer,state integer,
 activated_at_utc timestamptz,expires_at_utc timestamptz);
CREATE TABLE signing_key_versions(id uuid PRIMARY KEY,customer_user_id uuid,taxpayer_id uuid,fiscal_memory_id uuid,
 lifecycle_state smallint,version_number bigint,public_spki_der bytea);
CREATE TABLE signing_key_selections(fiscal_memory_id uuid,customer_user_id uuid,taxpayer_id uuid,key_version_id uuid);
""");
                await using var db = f.Context();
                await using (var tx = await db.Database.BeginTransactionAsync())
                {
                    foreach (var operation in new AddFiscalMemoryAuthorityEvents().UpOperations.OfType<SqlOperation>())
                        await db.Database.ExecuteSqlRawAsync(operation.Sql);
                    await tx.CommitAsync();
                }
                var now = DateTime.UtcNow;
                foreach (var (id, role, token) in new[] {
                    (f.Root, IdentityRoleNames.Level1, f.RootToken), (f.Customer, IdentityRoleNames.Customer, f.CustomerToken) })
                {
                    db.IdentityUsers.Add(new() { Id=id, IsActive=true, CreatedAtUtc=now,
                        NormalizedEmail=$"{id:N}@EXAMPLE.INVALID", Roles=[new() { UserId=id, Role=role, AssignedAtUtc=now }] });
                    db.IdentitySessions.Add(new() { Id=Guid.NewGuid(), UserId=id, AccessTokenHash=Hash(token),
                        RefreshTokenHash=Hash(Token()), CreatedAtUtc=now, AccessExpiresAtUtc=now.AddHours(1), RefreshExpiresAtUtc=now.AddHours(2) });
                }
                db.IdentityMfaStates.Add(new() { UserId=f.Root, EnabledAtUtc=now, EncryptedSecret="synthetic-not-used", RecoveryGeneration=1 });
                db.TaxpayerProfiles.Add(new() { Id=f.Taxpayer, CustomerUserId=f.Customer, Kind=TaxpayerKind.NaturalPerson,
                    NormalizedIdentifier=f.Identifier, DisplayName="Synthetic onboarding", Status=TaxpayerStatus.Active,
                    CreatedAtUtc=now, UpdatedAtUtc=now });
                db.CustomerTaxpayerMemberships.Add(new() { Id=Guid.NewGuid(), CustomerUserId=f.Customer,
                    TaxpayerId=f.Taxpayer, LinkedByUserId=f.Root, LinkedAtUtc=now });
                db.FiscalMemories.Add(new() { Id=f.Memory, TaxpayerId=f.Taxpayer, MemoryIdentifier="ABC123",
                    Status=FiscalMemoryStatus.PendingVerification, CreatedAtUtc=now, UpdatedAtUtc=now });
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO customer_subscriptions VALUES({Guid.NewGuid()},{f.Customer},1,2,{now.AddDays(-1)},{now.AddDays(2)});
INSERT INTO signing_key_versions VALUES({Guid.NewGuid()},{f.Customer},{f.Taxpayer},{f.Memory},2,1,{new byte[] { 1 }});
""");
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        public async Task ExecuteAsync(string sql)
        { await using var db=Context(); await db.Database.ExecuteSqlRawAsync(sql); }
        public async Task<long> ScalarAsync(string sql)
        {
            await using var db=Context(); await db.Database.OpenConnectionAsync();
            await using var command=db.Database.GetDbConnection().CreateCommand(); command.CommandText=sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        public async ValueTask DisposeAsync()
        {
            await using var root=new NpgsqlConnection(connection); await root.OpenAsync();
            await using var command=new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE",root);
            await command.ExecuteNonQueryAsync();
        }
        private static string Token() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
        private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    }
}

public sealed class Phase18OnboardingPostgresFactAttribute : FactAttribute
{
    public Phase18OnboardingPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHASE18_ONBOARDING_TEST_CONNECTION")) &&
            (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1" ||
             string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb"))))
            Skip="Requires explicit local PostgreSQL onboarding test connection; no authority I/O is used.";
    }
}
