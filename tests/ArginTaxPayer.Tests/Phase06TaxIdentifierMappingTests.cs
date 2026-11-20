using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06TaxIdentifierMappingTests
{
    [Fact]
    public void Catalog_uses_direct_thirteen_digit_code_without_mapping_history()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used")
                .Options);
        var item = db.Model.FindEntityType(typeof(CustomerCatalogItem))!;
        Assert.Equal(13, item.FindProperty(nameof(CustomerCatalogItem.OfficialCode))!.GetMaxLength());
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity =>
            entity.GetTableName() == "catalog_official_mappings");
    }
}
