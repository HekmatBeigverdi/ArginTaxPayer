using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

// Real PostgreSQL journal tests with synthetic custody only. Does not prove a deployed Vault.
public sealed class Phase09SigningGenerationPostgresTests
{
    private sealed record Fixture(DbContextOptions<ArginTaxPayerDbContext> Options,
        SigningScope Scope, string Token)
    {
        public ReserveSigningOperation Request(Guid? id = null) =>
            new(Scope, id ?? Guid.NewGuid(), SigningOperationKind.Generate,
                SigningOperationPolicy.LegacyProfile, SigningOperationPolicy.ConsentVersion, true);
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("An explicit test DB configuration is required.");
        if (new NpgsqlConnectionStringBuilder(cs).Database != "argintaxpayer_test")
            throw new InvalidOperationException("Refusing non-test database.");
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(cs, p => p.EnableRetryOnFailure(3)).Options;
        await using var db = new ArginTaxPayerDbContext(options);
        await db.Database.OpenConnectionAsync();
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT current_database();";
            Assert.Equal("argintaxpayer_test", (string?)await command.ExecuteScalarAsync());
        }
        Assert.Contains("20261020181500_AddPhase09SigningMetadata",
            await db.Database.GetAppliedMigrationsAsync());

        var now = DateTime.UtcNow;
        var customer = Guid.NewGuid();
        var taxpayer = Guid.NewGuid();
        var memory = Guid.NewGuid();
        var email = customer.ToString("N") + "@example.invalid";
        const string password = "Synthetic-signing-fixture-only-2026!";
        db.IdentityUsers.Add(new IdentityUser
        {
            Id = customer, NormalizedEmail = InitialAdministratorBootstrap.NormalizeEmail(email), IsActive = true,
            CreatedAtUtc = now, PasswordChangedAtUtc = now,
            PasswordHash = IdentityPasswordHasher.Hash(password),
            Roles = [new() { UserId = customer, Role = IdentityRoleNames.Customer, AssignedAtUtc = now }]
        });
        db.SoftwareUserProfiles.Add(new SoftwareUserProfile
        {
            UserId = customer, FirstName = "Synthetic", LastName = "Signing",
            Address = "Isolated test account", CompletedAtUtc = now, UpdatedAtUtc = now
        });
        db.TaxpayerProfiles.Add(new TaxpayerProfile
        {
            Id = taxpayer, CustomerUserId = customer, Kind = TaxpayerKind.LegalEntity,
            NormalizedIdentifier = customer.ToString("N"), DisplayName = "Synthetic signing fixture",
            Status = TaxpayerStatus.Active, Revision = 1, CreatedAtUtc = now, UpdatedAtUtc = now
        });
        db.FiscalMemories.Add(new FiscalMemory
        {
            Id = memory, TaxpayerId = taxpayer, MemoryIdentifier = memory.ToString("N").ToUpperInvariant(),
            Status = FiscalMemoryStatus.PendingVerification, Revision = 1,
            CreatedAtUtc = now, UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        var token = await new IdentitySessionService(db).LoginAsync(email, password);
        Assert.NotNull(token);
        return new(options, new(customer, taxpayer, memory), token!.AccessToken);
    }


    private static GenerateSigningKeyRequest Request(Fixture f, Guid? operation = null) =>
        new(f.Scope.TaxpayerId, f.Scope.FiscalMemoryId, operation ?? Guid.NewGuid(),
            SigningOperationPolicy.LegacyProfile, SigningOperationPolicy.ConsentVersion, true);

    private static SigningKeyGenerationService Service(ArginTaxPayerDbContext db,
        Phase09SigningKeyGenerationTests.Custody custody, Phase09SigningKeyGenerationTests.Factory factory) =>
        new((ISigningGenerationJournal)new EfSigningOperationRepository(db), custody, factory);

    [Phase09PostgresFact]
    public async Task Initial_generation_seals_with_atomic_audit_and_public_replay_without_selection()
    {
        var f = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        var factory = new Phase09SigningKeyGenerationTests.Factory();
        var service = Service(db, custody, factory);
        var request = Request(f);
        var first = await service.GenerateAsync(f.Token, request);
        Assert.Equal(SigningGenerationStatus.Created, first.Status);
        Assert.NotNull(first.PublicKey);
        Assert.False(first.PublicKey!.PortalUploadApproved);
        Assert.Equal(SigningGenerationStatus.Replayed, (await service.GenerateAsync(f.Token, request)).Status);
        Assert.Equal(1, factory.Calls);
        var key = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.CustomerUserId == f.Scope.CustomerUserId);
        Assert.Equal(2, key.LifecycleState);
        Assert.Equal(2, key.Revision);
        Assert.Equal(1, key.PortalState);
        var op = await db.SigningOperations.AsNoTracking().SingleAsync(o => o.Id == key.OriginOperationRecordId);
        Assert.Equal(3, op.State);
        Assert.Equal(3, op.Revision);
        Assert.Equal(6, await db.SigningAuditEvents.CountAsync(e => e.OperationRecordId == op.Id));
        Assert.False(await db.SigningKeySelections.AnyAsync(s => s.FiscalMemoryId == f.Scope.FiscalMemoryId));
        Assert.Equal(FiscalMemoryStatus.PendingVerification,
            (await db.FiscalMemories.AsNoTracking().SingleAsync(m => m.Id == f.Scope.FiscalMemoryId)).Status);
        Assert.Equal(SigningGenerationStatus.Replayed, (await service.ReadPublicAsync(f.Token,
            f.Scope.TaxpayerId, f.Scope.FiscalMemoryId, key.Id)).Status);
        var outsider = await CreateFixtureAsync();
        Assert.Equal(SigningGenerationStatus.Forbidden, (await service.ReadPublicAsync(outsider.Token,
            f.Scope.TaxpayerId, f.Scope.FiscalMemoryId, key.Id)).Status);
        Assert.Equal(SigningGenerationStatus.Conflict, (await service.GenerateAsync(f.Token, Request(f))).Status);
        Assert.Equal(1, factory.Calls); // A new OperationId must not bypass the future rotation review.
    }

    [Phase09PostgresFact]
    public async Task Concurrent_calls_have_one_crypto_winner_and_one_persisted_key()
    {
        var f = await CreateFixtureAsync();
        await using var one = new ArginTaxPayerDbContext(f.Options);
        await using var two = new ArginTaxPayerDbContext(f.Options);
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        var factory = new Phase09SigningKeyGenerationTests.Factory();
        var request = Request(f);
        var results = await Task.WhenAll(Service(one, custody, factory).GenerateAsync(f.Token, request),
            Service(two, custody, factory).GenerateAsync(f.Token, request));
        Assert.All(results, r => Assert.Contains(r.Status, new[] { SigningGenerationStatus.Created,
            SigningGenerationStatus.Replayed, SigningGenerationStatus.RecoveryRequired, SigningGenerationStatus.Conflict }));
        await using var check = new ArginTaxPayerDbContext(f.Options);
        Assert.Equal(SigningGenerationStatus.Replayed,
            (await Service(check, custody, factory).GenerateAsync(f.Token, request)).Status);
        Assert.Equal(1, factory.Calls);
        Assert.Equal(1, custody.Stages);
        Assert.Equal(1, await check.SigningKeyVersions.CountAsync(k => k.FiscalMemoryId == f.Scope.FiscalMemoryId));
    }

    [Phase09PostgresFact]
    public async Task Lost_provider_acknowledgement_retains_intent_and_recovers_same_key()
    {
        var f = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var custody = new Phase09SigningKeyGenerationTests.Custody { LoseAcknowledgement = true };
        var factory = new Phase09SigningKeyGenerationTests.Factory();
        var service = Service(db, custody, factory);
        var request = Request(f);
        Assert.Equal(SigningGenerationStatus.RecoveryRequired, (await service.GenerateAsync(f.Token, request)).Status);
        var staged = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.FiscalMemoryId == f.Scope.FiscalMemoryId);
        Assert.Equal(1, staged.LifecycleState);
        Assert.Null(staged.PublicSpkiDer);
        Assert.Equal(5, (await db.SigningOperations.AsNoTracking()
            .SingleAsync(o => o.Id == staged.OriginOperationRecordId)).State);
        var recovered = await service.GenerateAsync(f.Token, request);
        Assert.Equal(SigningGenerationStatus.Replayed, recovered.Status);
        Assert.Equal(staged.Id, recovered.PublicKey!.KeyVersionId);
        Assert.Equal(1, factory.Calls);
    }

    [Phase09PostgresFact]
    public async Task Foreign_owner_stale_login_and_revoked_session_cannot_generate()
    {
        var a = await CreateFixtureAsync();
        var b = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(a.Options);
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        var factory = new Phase09SigningKeyGenerationTests.Factory();
        var service = Service(db, custody, factory);
        Assert.Equal(SigningGenerationStatus.Forbidden, (await service.GenerateAsync(b.Token, Request(a))).Status);
        db.IdentityUserRoles.Add(new IdentityUserRole
        {
            UserId = b.Scope.CustomerUserId, Role = IdentityRoleNames.Level1, AssignedAtUtc = DateTime.UtcNow
        });
        db.IdentityMfaStates.Add(new IdentityMfaState
        {
            UserId = b.Scope.CustomerUserId, EncryptedSecret = "synthetic-only",
            EnabledAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        Assert.Equal(SigningGenerationStatus.Forbidden, (await service.GenerateAsync(b.Token, Request(a))).Status);
        await db.IdentitySessions.Where(s => s.UserId == a.Scope.CustomerUserId).ExecuteUpdateAsync(
            setters => setters.SetProperty(s => s.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-6)));
        Assert.Equal(SigningGenerationStatus.Forbidden, (await service.GenerateAsync(a.Token, Request(a))).Status);
        await db.IdentitySessions.Where(s => s.UserId == a.Scope.CustomerUserId).ExecuteUpdateAsync(
            setters => setters.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow));
        Assert.Equal(SigningGenerationStatus.Unauthenticated, (await service.GenerateAsync(a.Token, Request(a))).Status);
        Assert.Equal(0, factory.Calls);
        Assert.False(await db.SigningKeyVersions.AnyAsync(k => k.CustomerUserId == a.Scope.CustomerUserId));
    }

    [Phase09PostgresFact]
    public async Task Revocation_after_custody_blocks_commit_then_fresh_owner_recovers_without_regeneration()
    {
        var f = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        custody.AfterStage = async () =>
        {
            await using var revoke = new ArginTaxPayerDbContext(f.Options);
            await revoke.IdentitySessions.Where(s => s.UserId == f.Scope.CustomerUserId).ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow));
        };
        var factory = new Phase09SigningKeyGenerationTests.Factory();
        var service = Service(db, custody, factory);
        var request = Request(f);
        Assert.Equal(SigningGenerationStatus.Unauthenticated, (await service.GenerateAsync(f.Token, request)).Status);
        var key = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.FiscalMemoryId == f.Scope.FiscalMemoryId);
        Assert.Equal(1, key.LifecycleState);
        Assert.Null(key.PublicSpkiDer);
        var email = await db.IdentityUsers.Where(u => u.Id == f.Scope.CustomerUserId)
            .Select(u => u.NormalizedEmail).SingleAsync();
        var session = await new IdentitySessionService(db).LoginAsync(email, "Synthetic-signing-fixture-only-2026!");
        Assert.NotNull(session);
        var recovered = await service.GenerateAsync(session!.AccessToken, request);
        Assert.Equal(SigningGenerationStatus.Replayed, recovered.Status);
        Assert.Equal(key.Id, recovered.PublicKey!.KeyVersionId);
        Assert.Equal(1, factory.Calls);
    }
}
