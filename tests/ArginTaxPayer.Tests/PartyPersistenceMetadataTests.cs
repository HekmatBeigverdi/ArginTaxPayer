using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PartyPersistenceMetadataTests
{
    [Fact]
    public void Party_has_unique_identity_per_kind_and_revision_token()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(Party))!;
        Assert.Equal("parties", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(Party.Revision))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(Party.Kind), nameof(Party.NormalizedIdentifier) }));
    }

    [Fact]
    public void Natural_person_name_fields_are_optional_for_legacy_and_legal_parties()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(Party))!;
        Assert.True(entity.FindProperty(nameof(Party.FirstName))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(Party.LastName))!.IsNullable);
        Assert.Equal(100, entity.FindProperty(nameof(Party.FirstName))!.GetMaxLength());
        Assert.Equal(100, entity.FindProperty(nameof(Party.LastName))!.GetMaxLength());
    }

    [Fact]
    public void Legal_registration_number_is_nullable_and_bounded()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(Party))!;
        var property = entity.FindProperty(nameof(Party.RegistrationNumber))!;
        Assert.True(property.IsNullable);
        Assert.Equal(32, property.GetMaxLength());
    }

    [Fact]
    public void Customer_party_link_has_restrictive_foreign_keys_and_active_uniqueness()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(CustomerPartyLink))!;
        Assert.Equal("customer_party_links", entity.GetTableName());
        Assert.Equal(2, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.GetFilter() == "unlinked_at_utc IS NULL");
    }

    private static ArginTaxPayerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=never_used;Password=never_used")
            .Options);
}
