using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class TaxpayerPersistenceMetadataTests
{
    [Fact]
    public void Profile_has_unique_composite_identifier_and_is_not_identity_user()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused")
            .Options;
        using var db = new ArginTaxPayerDbContext(options);
        var entity = db.Model.FindEntityType(typeof(TaxpayerProfile))!;
        Assert.NotNull(entity);
        Assert.Equal("taxpayer_profiles", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "Kind", "NormalizedIdentifier" }));
        Assert.Null(entity.FindProperty("PasswordHash"));
        Assert.NotNull(db.Model.FindEntityType(typeof(ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser)));
    }

    [Fact]
    public void Lifecycle_rejects_archived_profile_reactivation()
    {
        Assert.False(TaxpayerLifecyclePolicy.CanTransition(TaxpayerStatus.Archived, TaxpayerStatus.Active));
    }
}
