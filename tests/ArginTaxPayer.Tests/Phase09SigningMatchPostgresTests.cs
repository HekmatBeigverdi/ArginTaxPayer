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
using ArginTaxPayer.Infrastructure.Signing.Matching;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningMatchPostgresTests
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



    private static async Task<MatchSigningKeyRequest> SeedAsync(Fixture f, RSA rsa)
    {
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var importer = new SigningKeyImportService((ISigningImportJournal)new EfSigningOperationRepository(db),
            new Phase09SigningKeyGenerationTests.Custody(), new());
        var imported = await importer.ImportAsync(f.Token, new(f.Scope.TaxpayerId, f.Scope.FiscalMemoryId,
            Guid.NewGuid(), SigningOperationPolicy.LegacyProfile, SigningOperationPolicy.ConsentVersion, true),
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der));
        Assert.Equal(SigningGenerationStatus.Created, imported.Status);
        return new(f.Scope.TaxpayerId, f.Scope.FiscalMemoryId, imported.PublicKey!.KeyVersionId,
            imported.PublicKey.Revision, SigningPublicKeyFormat.SpkiDer);
    }
    private static SigningKeyMatchService Service(ArginTaxPayerDbContext db, IKeyMatchProof proof) =>
        new((ISigningKeyMatchJournal)new EfSigningOperationRepository(db), proof, new());
    private sealed class Proof(RSA rsa) : IKeyMatchProof
    {
        public int Calls;
        public Func<Task>? AfterProof;
        public async Task<bool> ProveAsync(KeyMatchTicket ticket, byte[] spki, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            using var clear = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
            var matched = RsaKeyMatchProof.Match(clear.Bytes, ticket.StoredSpki, spki);
            if (AfterProof is not null) await AfterProof();
            return matched;
        }
    }

    [Phase09PostgresFact]
    public async Task Match_commits_two_audits_and_public_result_without_portal_or_selection_changes()
    {
        var f = await CreateFixtureAsync();
        using var rsa = RSA.Create(2048);
        var request = await SeedAsync(f, rsa);
        await using var db = new ArginTaxPayerDbContext(f.Options);
        // A retained, explicitly deselected row must not block this key.
        var origin = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.Id == request.KeyVersionId);
        var selection = new SigningKeySelection
        {
            FiscalMemoryId = f.Scope.FiscalMemoryId, CustomerUserId = f.Scope.CustomerUserId,
            TaxpayerId = f.Scope.TaxpayerId, KeyVersionId = null, Revision = 1,
            SelectedAtUtc = DateTime.UtcNow, SelectedByUserId = f.Scope.CustomerUserId
        };
        db.SigningKeySelections.Add(selection);
        void AuditSelection(string outcome) => db.SigningAuditEvents.Add(new SigningAuditEvent
        {
            Id = Guid.NewGuid(), CustomerUserId = f.Scope.CustomerUserId, TaxpayerId = f.Scope.TaxpayerId,
            FiscalMemoryId = f.Scope.FiscalMemoryId, OperationRecordId = origin.OriginOperationRecordId,
            ActorUserId = f.Scope.CustomerUserId, SubjectKind = 3, SubjectId = f.Scope.FiscalMemoryId,
            Revision = selection.Revision, ActionCode = "synthetic_selection", OutcomeCode = outcome,
            OccurredAtUtc = selection.SelectedAtUtc
        });
        AuditSelection("Cleared");
        await db.SaveChangesAsync();
        var proof = new Proof(rsa);
        var result = await Service(db, proof).MatchAsync(f.Token, request with { Format = SigningPublicKeyFormat.RsaPublicPem },
            Phase09SigningPublicMatchTests.PublicInput(rsa, SigningPublicKeyFormat.RsaPublicPem));
        Assert.Equal(SigningKeyMatchStatus.Matched, result.Status);
        Assert.Equal(result.StoredFingerprintSha256, result.ProvidedFingerprintSha256);
        Assert.False(result.PortalRegistrationProven);
        var key = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.Id == request.KeyVersionId);
        Assert.Equal(2, key.PublicMatch);
        Assert.Equal(2, key.LifecycleState);
        Assert.Equal(1, key.PortalState);
        Assert.Equal(request.ExpectedRevision + 2, key.Revision);
        Assert.Equal(key.Revision, result.Revision);
        Assert.Equal(2, await db.SigningAuditEvents.CountAsync(a => a.SubjectId == key.Id && a.ActionCode.StartsWith("public_match_")));
        selection = await db.SigningKeySelections.SingleAsync(s => s.FiscalMemoryId == f.Scope.FiscalMemoryId);
        Assert.Null(selection.KeyVersionId);
        Assert.Equal(1, selection.Revision);
        var memory = await db.FiscalMemories.AsNoTracking().SingleAsync(m => m.Id == f.Scope.FiscalMemoryId);
        Assert.Equal(FiscalMemoryStatus.PendingVerification, memory.Status);
        Assert.Equal(1, memory.Revision);
        Assert.Equal(SigningKeyMatchStatus.Conflict, (await Service(db, proof).MatchAsync(f.Token,
            request, rsa.ExportSubjectPublicKeyInfo())).Status);
        Assert.Equal(1, proof.Calls);
        // Selecting this exact key blocks a new comparison without private admission.
        selection = await db.SigningKeySelections.SingleAsync(s => s.FiscalMemoryId == f.Scope.FiscalMemoryId);
        selection.KeyVersionId = key.Id;
        selection.Revision++;
        selection.SelectedAtUtc = DateTime.UtcNow;
        AuditSelection("SelectedLocal");
        await db.SaveChangesAsync();
        Assert.Equal(SigningKeyMatchStatus.Conflict, (await Service(db, proof).MatchAsync(f.Token,
            request with { ExpectedRevision = key.Revision }, rsa.ExportSubjectPublicKeyInfo())).Status);
        Assert.Equal(1, proof.Calls);
    }

    [Phase09PostgresFact]
    public async Task Mismatch_suspends_and_later_correct_match_does_not_resume()
    {
        var f = await CreateFixtureAsync();
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var request = await SeedAsync(f, rsa);
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var service = Service(db, new Proof(rsa));
        var mismatch = await service.MatchAsync(f.Token, request, other.ExportSubjectPublicKeyInfo());
        Assert.Equal(SigningKeyMatchStatus.Mismatch, mismatch.Status);
        Assert.NotEqual(mismatch.StoredFingerprintSha256, mismatch.ProvidedFingerprintSha256);
        var key = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.Id == request.KeyVersionId);
        Assert.Equal(3, key.PublicMatch);
        Assert.Equal(3, key.LifecycleState);
        var match = await service.MatchAsync(f.Token, request with { ExpectedRevision = key.Revision }, rsa.ExportSubjectPublicKeyInfo());
        Assert.Equal(SigningKeyMatchStatus.Matched, match.Status);
        key = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.Id == request.KeyVersionId);
        Assert.Equal(2, key.PublicMatch);
        Assert.Equal(3, key.LifecycleState);
        Assert.Equal(1, key.PortalState);
        Assert.Equal(4, await db.SigningAuditEvents.CountAsync(a => a.SubjectId == key.Id && a.ActionCode.StartsWith("public_match_")));
    }

    [Phase09PostgresFact]
    public async Task Foreign_stale_and_revoked_sessions_are_denied_before_private_access()
    {
        var f = await CreateFixtureAsync();
        var foreign = await CreateFixtureAsync();
        using var rsa = RSA.Create(2048);
        var request = await SeedAsync(f, rsa);
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var proof = new Proof(rsa);
        var service = Service(db, proof);
        Assert.Equal(SigningKeyMatchStatus.Forbidden, (await service.MatchAsync(foreign.Token, request, new byte[] { 1 })).Status);
        await db.IdentitySessions.Where(s => s.UserId == f.Scope.CustomerUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-10)));
        Assert.Equal(SigningKeyMatchStatus.Forbidden, (await service.MatchAsync(f.Token, request, new byte[] { 1 })).Status);
        await db.IdentitySessions.Where(s => s.UserId == f.Scope.CustomerUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow));
        Assert.Equal(SigningKeyMatchStatus.Unauthenticated, (await service.MatchAsync(f.Token, request, new byte[] { 1 })).Status);
        Assert.Equal(0, proof.Calls);
        Assert.False(await db.SigningAuditEvents.AnyAsync(a => a.SubjectId == request.KeyVersionId && a.ActionCode == "public_match_intent"));
    }

    [Phase09PostgresFact]
    public async Task Concurrent_same_revision_has_one_winner_and_one_private_admission()
    {
        var f = await CreateFixtureAsync();
        using var rsa = RSA.Create(2048);
        var request = await SeedAsync(f, rsa);
        var proof = new Proof(rsa);
        var spki = rsa.ExportSubjectPublicKeyInfo();
        async Task<SigningKeyMatchResult> Run()
        {
            await using var context = new ArginTaxPayerDbContext(f.Options);
            return await Service(context, proof).MatchAsync(f.Token, request, spki);
        }
        var results = await Task.WhenAll(Run(), Run());
        Assert.Single(results.Where(x => x.Status == SigningKeyMatchStatus.Matched));
        Assert.Single(results.Where(x => x.Status == SigningKeyMatchStatus.Conflict));
        Assert.Equal(1, proof.Calls);
        await using var db = new ArginTaxPayerDbContext(f.Options);
        Assert.Equal(2, await db.SigningAuditEvents.CountAsync(a => a.SubjectId == request.KeyVersionId && a.ActionCode.StartsWith("public_match_")));
    }

    [Phase09PostgresFact]
    public async Task Revocation_after_proof_denies_result_and_fresh_owner_can_retry_current_revision()
    {
        var f = await CreateFixtureAsync();
        using var rsa = RSA.Create(2048);
        var request = await SeedAsync(f, rsa);
        await using var db = new ArginTaxPayerDbContext(f.Options);
        var proof = new Proof(rsa)
        {
            AfterProof = async () =>
            {
                await using var other = new ArginTaxPayerDbContext(f.Options);
                await other.IdentitySessions.Where(s => s.UserId == f.Scope.CustomerUserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow));
            }
        };
        var result = await Service(db, proof).MatchAsync(f.Token, request, rsa.ExportSubjectPublicKeyInfo());
        Assert.Equal(SigningKeyMatchStatus.Unauthenticated, result.Status);
        Assert.Null(result.StoredFingerprintSha256);
        var key = await db.SigningKeyVersions.AsNoTracking().SingleAsync(k => k.Id == request.KeyVersionId);
        Assert.Equal(1, key.PublicMatch);
        Assert.Equal(request.ExpectedRevision + 1, key.Revision);
        Assert.Single(await db.SigningAuditEvents.Where(a => a.SubjectId == key.Id && a.ActionCode.StartsWith("public_match_")).ToListAsync());
        var email = await db.IdentityUsers.Where(u => u.Id == f.Scope.CustomerUserId).Select(u => u.NormalizedEmail).SingleAsync();
        var login = await new IdentitySessionService(db).LoginAsync(email, "Synthetic-signing-fixture-only-2026!");
        Assert.NotNull(login);
        var retried = await Service(db, new Proof(rsa)).MatchAsync(login!.AccessToken,
            request with { ExpectedRevision = key.Revision }, rsa.ExportSubjectPublicKeyInfo());
        Assert.Equal(SigningKeyMatchStatus.Matched, retried.Status);
        Assert.False(retried.PortalRegistrationProven);
        // A concurrent lifecycle safety stop invalidates an already admitted proof ticket.
        var journal = (ISigningKeyMatchJournal)new EfSigningOperationRepository(db);
        var admission = await journal.BeginMatchAsync(login.AccessToken,
            request with { ExpectedRevision = retried.Revision!.Value }, retried.StoredFingerprintSha256!, default);
        Assert.NotNull(admission.Ticket);
        await using (var concurrent = new ArginTaxPayerDbContext(f.Options))
        {
            var changed = await concurrent.SigningKeyVersions.SingleAsync(k => k.Id == request.KeyVersionId);
            changed.LifecycleState = 3;
            changed.Revision++;
            changed.UpdatedAtUtc = DateTime.UtcNow;
            concurrent.SigningAuditEvents.Add(new SigningAuditEvent
            {
                Id = Guid.NewGuid(), CustomerUserId = changed.CustomerUserId, TaxpayerId = changed.TaxpayerId,
                FiscalMemoryId = changed.FiscalMemoryId, OperationRecordId = changed.OriginOperationRecordId,
                ActorUserId = f.Scope.CustomerUserId, SubjectKind = 2, SubjectId = changed.Id,
                Revision = changed.Revision, ActionCode = "synthetic_safety_stop", OutcomeCode = "Suspended",
                PublicFingerprintSha256 = changed.PublicFingerprintSha256, OccurredAtUtc = changed.UpdatedAtUtc
            });
            await concurrent.SaveChangesAsync();
        }
        Assert.Equal(SigningKeyMatchStatus.Conflict,
            (await journal.CompleteMatchAsync(login.AccessToken, admission.Ticket!, true, default)).Status);

    }
}
