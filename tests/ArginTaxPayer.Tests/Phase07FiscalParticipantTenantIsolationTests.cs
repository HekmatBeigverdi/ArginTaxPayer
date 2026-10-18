using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07FiscalParticipantTenantIsolationTests
{
    private static ArginTaxPayerDbContext Context() => new(
        new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=argintaxpayer_test;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Scoped_identity_migration_is_registered_without_network_access()
    {
        using var db = Context();
        Assert.Contains("20261018090000_ScopeFiscalParticipantsPerCustomer",
            db.Database.GetMigrations());
    }

    [Fact]
    public void Buyer_uniqueness_is_per_customer_not_global()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(Party))!;
        Assert.Equal("owner_customer_user_id",
            entity.FindProperty(nameof(Party.OwnerCustomerUserId))!
                .GetColumnName());
        var unique = Assert.Single(entity.GetIndexes().Where(x =>
            x.GetDatabaseName() == "ux_parties_owner_kind_identifier"));
        Assert.True(unique.IsUnique);
        Assert.Equal(new[] { "OwnerCustomerUserId", "Kind", "NormalizedIdentifier" },
            unique.Properties.Select(p => p.Name).ToArray());
        Assert.Equal("owner_customer_user_id IS NOT NULL", unique.GetFilter());
        Assert.DoesNotContain(entity.GetIndexes(), x =>
            x.GetDatabaseName() == "ux_parties_kind_identifier");
    }

    [Fact]
    public void Taxpayer_uniqueness_is_scoped_and_legacy_nulls_are_retained()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(TaxpayerProfile))!;
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.GetDatabaseName() == "ux_taxpayer_profiles_customer_kind_identifier" &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { "CustomerUserId", "Kind", "NormalizedIdentifier" }));
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.GetDatabaseName() == "ux_taxpayer_profiles_legacy_identifier" &&
            x.GetFilter() == "customer_user_id IS NULL");
        Assert.DoesNotContain(entity.GetIndexes(), x =>
            x.GetDatabaseName() == "ux_taxpayer_profiles_kind_identifier");
    }

    [Fact]
    public void Owner_reference_is_restricted_instead_of_cascading_deletion()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(Party))!;
        Assert.Contains(entity.GetForeignKeys(), key =>
            key.Properties.Any(p => p.Name == "OwnerCustomerUserId") &&
            key.DeleteBehavior == DeleteBehavior.Restrict);
    }
}
