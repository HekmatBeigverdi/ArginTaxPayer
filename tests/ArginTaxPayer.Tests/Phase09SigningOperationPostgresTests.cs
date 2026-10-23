using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09PostgresFactAttribute : FactAttribute
{
    public Phase09PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1")
            Skip = "Opt-in PostgreSQL test; requires the approved Phase09 migration on argintaxpayer_test.";
    }
}

/// <summary>Uses synthetic fixture accounts in the explicitly configured test DB.
/// Append-only journal/consent/audit records are intentionally retained, not deleted or truncated.
/// No migration or provider operation is performed by these tests.</summary>
public sealed class Phase09SigningOperationPostgresTests
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
            Id = customer, NormalizedEmail = email, IsActive = true,
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

    [Phase09PostgresFact]
    public async Task Reservation_consent_audit_and_replay_are_atomic_and_conflicting_metadata_is_rejected()
    {
        var f = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var repository = new EfSigningOperationRepository(db);
        var request = f.Request();
        var first = await repository.ReserveAsync(f.Token, request);
        Assert.Equal(SigningPersistenceStatus.Created, first.Status);
        Assert.Equal(SigningPersistenceStatus.Replayed, (await repository.ReserveAsync(f.Token, request)).Status);
        var changed = request with
        {
            Kind = SigningOperationKind.Import,
            ExpectedPublicFingerprintSha256 = new string('a', 64)
        };
        Assert.Equal(SigningPersistenceStatus.Conflict, (await repository.ReserveAsync(f.Token, changed)).Status);
        var id = first.Operation!.RecordId;
        Assert.Equal(1, await db.SigningOperations.CountAsync(o => o.Id == id));
        Assert.Equal(1, await db.SigningConsents.CountAsync(c => c.OperationRecordId == id));
        Assert.Equal(2, await db.SigningAuditEvents.CountAsync(e => e.OperationRecordId == id));
        Assert.Null(first.Operation.KeyVersionId); // No key or provider work occurred.
    }

    [Phase09PostgresFact]
    public async Task Recovery_journal_replays_transitions_and_refuses_success_without_a_sealed_key()
    {
        var f = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var repository = new EfSigningOperationRepository(db);
        var request = f.Request();
        await repository.ReserveAsync(f.Token, request);
        var recovery = await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
            1, SigningOperationState.RecoveryRequired, SigningFailure.ProviderOutcomeUnknown);
        Assert.Equal(SigningPersistenceStatus.Updated, recovery.Status);
        Assert.Equal(SigningPersistenceStatus.Replayed,
            (await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
                1, SigningOperationState.RecoveryRequired, SigningFailure.ProviderOutcomeUnknown)).Status);
        Assert.Equal(SigningPersistenceStatus.Updated,
            (await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
                2, SigningOperationState.VaultStaged)).Status);
        Assert.Equal(SigningPersistenceStatus.Conflict,
            (await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
                3, SigningOperationState.Committed)).Status);
        var failed = await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
            3, SigningOperationState.Failed, SigningFailure.Cancelled);
        Assert.Equal(SigningPersistenceStatus.Updated, failed.Status);
        Assert.Equal(4, failed.Operation!.Revision);
        Assert.Equal(SigningPersistenceStatus.Conflict,
            (await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
                4, SigningOperationState.VaultStaged)).Status);
        Assert.Equal(5, await db.SigningAuditEvents.CountAsync(e =>
            e.OperationRecordId == failed.Operation.RecordId)); // Four operation revisions + consent.
    }

    [Phase09PostgresFact]
    public async Task Missing_consent_cross_customer_and_revoked_session_never_create_claims()
    {
        var a = await CreateFixtureAsync();
        var b = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(a.Options);
        var repository = new EfSigningOperationRepository(db);
        var request = a.Request();
        Assert.Equal(SigningPersistenceStatus.Invalid,
            (await repository.ReserveAsync(a.Token, request with { AcceptServerCustody = false })).Status);
        Assert.Equal(SigningPersistenceStatus.Forbidden,
            (await repository.ReserveAsync(b.Token, request)).Status);
        await db.IdentitySessions.Where(s => s.UserId == a.Scope.CustomerUserId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow));
        Assert.Equal(SigningPersistenceStatus.Unauthenticated,
            (await repository.ReserveAsync(a.Token, request)).Status);
        Assert.Equal(0, await db.SigningOperations.CountAsync(o => o.CustomerUserId == a.Scope.CustomerUserId));
    }

    [Phase09PostgresFact]
    public async Task Concurrent_identical_claims_persist_once_and_retry_replays_the_winner()
    {
        var f = await CreateFixtureAsync();
        var request = f.Request();
        await using var one = new ArginTaxPayerDbContext(f.Options);
        await using var two = new ArginTaxPayerDbContext(f.Options);
        var results = await Task.WhenAll(
            new EfSigningOperationRepository(one).ReserveAsync(f.Token, request),
            new EfSigningOperationRepository(two).ReserveAsync(f.Token, request));
        Assert.Single(results.Where(r => r.Status == SigningPersistenceStatus.Created));
        Assert.All(results, r => Assert.Contains(r.Status, new[]
        {
            SigningPersistenceStatus.Created, SigningPersistenceStatus.Replayed, SigningPersistenceStatus.Conflict
        }));
        await using var check = new ArginTaxPayerDbContext(f.Options);
        Assert.Equal(SigningPersistenceStatus.Replayed,
            (await new EfSigningOperationRepository(check).ReserveAsync(f.Token, request)).Status);
        Assert.Equal(1, await check.SigningOperations.CountAsync(o =>
            o.CustomerUserId == f.Scope.CustomerUserId && o.OperationId == request.OperationId));
    }

    [Phase09PostgresFact]
    public async Task Deferred_guard_rolls_back_a_claim_without_audit_and_audit_cannot_be_edited()
    {
        var f = await CreateFixtureAsync();
        // Direct writes deliberately exercise the DB invariant instead of repository behavior.
        await using (var db = new ArginTaxPayerDbContext(f.Options))
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                db.SigningOperations.Add(new SigningOperation
                {
                    Id = Guid.NewGuid(), CustomerUserId = f.Scope.CustomerUserId,
                    TaxpayerId = f.Scope.TaxpayerId, FiscalMemoryId = f.Scope.FiscalMemoryId,
                    OperationId = Guid.NewGuid(), ActorUserId = f.Scope.CustomerUserId,
                    OperationKind = 1, ProfileId = SigningOperationPolicy.LegacyProfile,
                    MetadataDigestSha256 = new string('a',64), State = 1, Revision = 1,
                    CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
                var error = await Assert.ThrowsAsync<PostgresException>(() => tx.CommitAsync());
                Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            });
        }
        await using var valid = new ArginTaxPayerDbContext(f.Options);
        Assert.Equal(0, await valid.SigningOperations.CountAsync(o => o.CustomerUserId == f.Scope.CustomerUserId));
        var result = await new EfSigningOperationRepository(valid).ReserveAsync(f.Token, f.Request());
        var auditId = await valid.SigningAuditEvents.Where(e =>
            e.OperationRecordId == result.Operation!.RecordId).Select(e => e.Id).FirstAsync();
        var immutable = await Assert.ThrowsAsync<PostgresException>(() =>
            valid.SigningAuditEvents.Where(e => e.Id == auditId)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.ActionCode, "tampered")));
        Assert.Equal(PostgresErrorCodes.CheckViolation, immutable.SqlState);
    }

    [Phase09PostgresFact]
    public async Task Completion_replays_only_after_matching_scoped_sealed_public_metadata()
    {
        var f = await CreateFixtureAsync();
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var repository = new EfSigningOperationRepository(db);
        var request = f.Request();
        var reservation = await repository.ReserveAsync(f.Token, request);
        Assert.Equal(SigningPersistenceStatus.Created, reservation.Status);
        var operation = reservation.Operation!.RecordId;
        var keyId = Guid.NewGuid();
        using var rsa = RSA.Create(2048);
        var publicBytes = rsa.ExportSubjectPublicKeyInfo();
        var fingerprint = Convert.ToHexString(SHA256.HashData(publicBytes)).ToLowerInvariant();
        var now = DateTime.UtcNow;
        // Synthetic protected-store locator; no real vault is contacted.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            db.SigningKeyVersions.Add(new SigningKeyVersion
            {
                Id = keyId, CustomerUserId = f.Scope.CustomerUserId, TaxpayerId = f.Scope.TaxpayerId,
                FiscalMemoryId = f.Scope.FiscalMemoryId, OriginOperationRecordId = operation,
                Origin = 1, VersionNumber = 1, ProfileId = SigningOperationPolicy.LegacyProfile,
                PublicFingerprintSha256 = fingerprint, PublicSpkiDer = publicBytes,
                CustodyReference = "vault://fixture/" + keyId.ToString("N"), ProtectionKeyVersion = "fixture-v1",
                LifecycleState = 2, PortalState = 1, PublicMatch = 1, Revision = 1,
                CreatedAtUtc = now, UpdatedAtUtc = now
            });
            db.SigningAuditEvents.Add(new SigningAuditEvent
            {
                Id = Guid.NewGuid(), CustomerUserId = f.Scope.CustomerUserId, TaxpayerId = f.Scope.TaxpayerId,
                FiscalMemoryId = f.Scope.FiscalMemoryId, OperationRecordId = operation,
                ActorUserId = f.Scope.CustomerUserId, SubjectKind = 2, SubjectId = keyId, Revision = 1,
                ActionCode = "synthetic_key_sealed", OutcomeCode = "fixture",
                PublicFingerprintSha256 = fingerprint, OccurredAtUtc = now
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        });
        db.ChangeTracker.Clear();
        Assert.Equal(SigningPersistenceStatus.Updated,
            (await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
                1, SigningOperationState.VaultStaged)).Status);
        var committed = await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
            2, SigningOperationState.Committed);
        Assert.Equal(SigningPersistenceStatus.Updated, committed.Status);
        Assert.Equal(keyId, committed.Operation!.KeyVersionId);
        Assert.Equal(fingerprint, committed.Operation.PublicFingerprintSha256);
        Assert.Equal(SigningPersistenceStatus.Replayed,
            (await repository.AdvanceAsync(f.Token, f.Scope, request.OperationId,
                2, SigningOperationState.Committed)).Status);
    }

}
