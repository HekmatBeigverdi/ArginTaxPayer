using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Invalid input must be rejected before opening any database connection.</summary>
public sealed class Phase09SigningOperationValidationTests
{
    private static ArginTaxPayerDbContext Create() => new(
        new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=unused;Password=unused")
            .Options);

    [Fact]
    public async Task Missing_consent_and_unapproved_profile_are_rejected_before_database_access()
    {
        using var db = Create();
        var repo = new EfSigningOperationRepository(db);
        var request = new ReserveSigningOperation(
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(),
            SigningOperationKind.Generate, SigningOperationPolicy.LegacyProfile,
            SigningOperationPolicy.ConsentVersion, false);
        Assert.Equal(SigningPersistenceStatus.Invalid, (await repo.ReserveAsync(null, request)).Status);
        Assert.Equal(SigningPersistenceStatus.Invalid,
            (await repo.ReserveAsync(null, request with
                { AcceptServerCustody = true, ProfileId = "unapproved" })).Status);
    }

    [Fact]
    public async Task Import_requires_a_canonical_public_fingerprint()
    {
        using var db = Create();
        var repo = new EfSigningOperationRepository(db);
        var request = new ReserveSigningOperation(
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(),
            SigningOperationKind.Import, SigningOperationPolicy.LegacyProfile,
            SigningOperationPolicy.ConsentVersion, true);
        Assert.Equal(SigningPersistenceStatus.Invalid, (await repo.ReserveAsync(null, request)).Status);
    }

    [Fact]
    public async Task Invalid_revision_or_error_target_cannot_reach_database()
    {
        using var db = Create();
        var repo = new EfSigningOperationRepository(db);
        var scope = new SigningScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(SigningPersistenceStatus.Invalid,
            (await repo.AdvanceAsync(null, scope, Guid.NewGuid(), 0,
                SigningOperationState.Failed, SigningFailure.Cancelled)).Status);
        Assert.Equal(SigningPersistenceStatus.Invalid,
            (await repo.AdvanceAsync(null, scope, Guid.NewGuid(), 1,
                SigningOperationState.RecoveryRequired)).Status);
        Assert.Equal(SigningPersistenceStatus.Invalid,
            (await repo.AdvanceAsync(null, scope, Guid.NewGuid(), 1,
                SigningOperationState.VaultStaged, SigningFailure.Cancelled)).Status);
    }
}
