using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06CatalogPersistenceTests
{
    private static ArginTaxPayerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used")
            .Options);

    [Fact]
    public void Customer_catalog_codes_are_unique_inside_customer_scope()
    {
        using var db = CreateContext();
        var item = db.Model.FindEntityType(typeof(CustomerCatalogItem))!;
        Assert.Equal("customer_catalog_items", item.GetTableName());
        Assert.True(item.FindProperty(nameof(CustomerCatalogItem.Revision))!.IsConcurrencyToken);
        Assert.Contains(item.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(CustomerCatalogItem.CustomerUserId), nameof(CustomerCatalogItem.InternalCode) }));
        Assert.All(item.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void Official_codes_and_units_are_version_scoped_and_restrict_deletion()
    {
        using var db = CreateContext();
        foreach (var type in new[] { typeof(OfficialMeasurementUnit), typeof(OfficialGoodsServiceEntry) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
                x.Properties.Any(p => p.Name == "ReleaseId"));
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        }
        Assert.Equal(8, db.Model.FindEntityType(typeof(OfficialMeasurementUnit))!
            .FindProperty(nameof(OfficialMeasurementUnit.Code))!.GetMaxLength());
    }

    [Fact]
    public void Conversion_ratios_are_exact_integral_parts_not_floating_point()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(CatalogItemUnitConversion))!;
        Assert.Equal(typeof(long), entity.FindProperty(nameof(CatalogItemUnitConversion.ToBaseNumerator))!.ClrType);
        Assert.Equal(typeof(long), entity.FindProperty(nameof(CatalogItemUnitConversion.ToBaseDenominator))!.ClrType);
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void Vat_rates_are_nullable_and_precision_bound()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(VatClassificationEntry))!;
        var property = entity.FindProperty(nameof(VatClassificationEntry.RatePercent))!;
        Assert.True(property.IsNullable);
        Assert.Equal(8, property.GetPrecision());
        Assert.Equal(4, property.GetScale());
    }

    [Fact]
    public void Mapping_does_not_own_or_overwrite_legacy_entities()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(CatalogOfficialMapping))!;
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.DoesNotContain(entity.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType.Name == "TaxpayerProfile");
    }
}
