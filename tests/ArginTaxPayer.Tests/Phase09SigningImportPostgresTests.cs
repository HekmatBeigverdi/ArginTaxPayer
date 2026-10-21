using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Import;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningImportPostgresTests
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



    private static ImportSigningKeyRequest Request(Fixture f) => new(f.Scope.TaxpayerId, f.Scope.FiscalMemoryId,
        Guid.NewGuid(), SigningOperationPolicy.LegacyProfile, SigningOperationPolicy.ConsentVersion, true);
    private static SigningKeyImportService Service(ArginTaxPayerDbContext db,
        Phase09SigningKeyGenerationTests.Custody custody) => new(
            (ISigningImportJournal)new EfSigningOperationRepository(db), custody, new());

    [Phase09PostgresFact]
    public async Task Import_seals_atomically_replays_same_public_key_and_conflicts_on_changed_key()
    {
        var f = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        var service = Service(db, custody);
        using var rsa = RSA.Create(2048);
        var request = Request(f);
        var result = await service.ImportAsync(f.Token, request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Pem));
        Assert.Equal(SigningGenerationStatus.Created, result.Status);
        Assert.False(result.PublicKey!.PortalUploadApproved);
        Assert.Equal(SigningGenerationStatus.Replayed, (await service.ImportAsync(f.Token, request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.EncryptedPkcs8Der))).Status);
        using var different = RSA.Create(2048);
        Assert.Equal(SigningGenerationStatus.Conflict, (await service.ImportAsync(f.Token, request,
            Phase09SigningKeyImportTests.Input(different, SigningPrivateKeyFormat.Pkcs8Der))).Status);
        Assert.Equal(1, custody.Stages);
        var key = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.FiscalMemoryId == f.Scope.FiscalMemoryId);
        Assert.Equal(2, key.Origin);
        Assert.Equal(2, key.LifecycleState);
        Assert.Equal(1, key.PortalState);
        Assert.Equal(1, key.PublicMatch);
        Assert.Equal(rsa.ExportSubjectPublicKeyInfo(), key.PublicSpkiDer);
        var op = await db.SigningOperations.AsNoTracking().SingleAsync(o => o.Id == key.OriginOperationRecordId);
        Assert.Equal(2, op.OperationKind);
        Assert.Equal(3, op.State);
        Assert.Equal(6, await db.SigningAuditEvents.CountAsync(e => e.OperationRecordId == op.Id));
        Assert.False(await db.SigningKeySelections.AnyAsync(s => s.FiscalMemoryId == f.Scope.FiscalMemoryId));
        Assert.Equal(FiscalMemoryStatus.PendingVerification,
            (await db.FiscalMemories.AsNoTracking().SingleAsync(m => m.Id == f.Scope.FiscalMemoryId)).Status);
    }

    [Phase09PostgresFact]
    public async Task Lost_acknowledgement_recovers_same_import_without_second_stage()
    {
        var f = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var custody = new Phase09SigningKeyGenerationTests.Custody { LoseAcknowledgement = true };
        var service = Service(db, custody);
        using var rsa = RSA.Create(2048);
        var request = Request(f);
        Assert.Equal(SigningGenerationStatus.RecoveryRequired, (await service.ImportAsync(f.Token, request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der))).Status);
        var staged = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.FiscalMemoryId == f.Scope.FiscalMemoryId);
        Assert.Equal(1, staged.LifecycleState);
        Assert.Null(staged.PublicSpkiDer);
        var recovered = await service.ImportAsync(f.Token, request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.EncryptedPkcs8Pem));
        Assert.Equal(SigningGenerationStatus.Replayed, recovered.Status);
        Assert.Equal(staged.Id, recovered.PublicKey!.KeyVersionId);
        Assert.Equal(1, custody.Stages);
    }

    [Phase09PostgresFact]
    public async Task Foreign_owner_stale_login_and_revocation_deny_before_parsing_invalid_private_input()
    {
        var a = await CreateFixtureAsync();
        var b = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(a.Options);
        var service = Service(db, new());
        SensitiveKeyInput Bad() => new(SigningPrivateKeyFormat.Pkcs8Der, new byte[] { 1, 2, 3 });
        Assert.Equal(SigningGenerationStatus.Forbidden, (await service.ImportAsync(b.Token, Request(a), Bad())).Status);
        await db.IdentitySessions.Where(s => s.UserId == a.Scope.CustomerUserId).ExecuteUpdateAsync(
            setters => setters.SetProperty(s => s.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-6)));
        Assert.Equal(SigningGenerationStatus.Forbidden, (await service.ImportAsync(a.Token, Request(a), Bad())).Status);
        await db.IdentitySessions.Where(s => s.UserId == a.Scope.CustomerUserId).ExecuteUpdateAsync(
            setters => setters.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow));
        Assert.Equal(SigningGenerationStatus.Unauthenticated, (await service.ImportAsync(a.Token, Request(a), Bad())).Status);
        Assert.False(await db.SigningOperations.AnyAsync(o => o.CustomerUserId == a.Scope.CustomerUserId));
    }

    [Phase09PostgresFact]
    public async Task Concurrent_same_key_imports_persist_one_key_and_one_custody_stage()
    {
        var f = await CreateFixtureAsync();
        await using var one = new ArginTaxPayerDbContext(f.Options);
        await using var two = new ArginTaxPayerDbContext(f.Options);
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        using var rsa = RSA.Create(2048);
        var request = Request(f);
        var results = await Task.WhenAll(
            Service(one, custody).ImportAsync(f.Token, request, Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der)),
            Service(two, custody).ImportAsync(f.Token, request, Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der)));
        Assert.All(results, r => Assert.Contains(r.Status, new[] { SigningGenerationStatus.Created,
            SigningGenerationStatus.Replayed, SigningGenerationStatus.RecoveryRequired, SigningGenerationStatus.Conflict }));
        await using var check = new ArginTaxPayerDbContext(f.Options);
        Assert.Equal(SigningGenerationStatus.Replayed, (await Service(check, custody).ImportAsync(f.Token, request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der))).Status);
        Assert.Equal(1, custody.Stages);
        Assert.Equal(1, await check.SigningKeyVersions.CountAsync(k => k.FiscalMemoryId == f.Scope.FiscalMemoryId));
    }

    [Phase09PostgresFact]
    public async Task Revocation_after_import_custody_blocks_commit_until_fresh_owner_recovery()
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
        var service = Service(db, custody);
        using var rsa = RSA.Create(2048);
        var request = Request(f);
        Assert.Equal(SigningGenerationStatus.Unauthenticated, (await service.ImportAsync(f.Token, request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der))).Status);
        var staged = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.FiscalMemoryId == f.Scope.FiscalMemoryId);
        Assert.Equal(1, staged.LifecycleState);
        var email = await db.IdentityUsers.Where(u => u.Id == f.Scope.CustomerUserId).Select(u => u.NormalizedEmail).SingleAsync();
        var fresh = await new IdentitySessionService(db).LoginAsync(email, "Synthetic-signing-fixture-only-2026!");
        Assert.NotNull(fresh);
        Assert.Equal(SigningGenerationStatus.Replayed, (await service.ImportAsync(fresh!.AccessToken, request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der))).Status);
        Assert.Equal(1, custody.Stages);
    }
}
