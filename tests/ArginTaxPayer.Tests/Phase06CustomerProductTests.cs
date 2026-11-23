using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06CustomerProductTests
{
    private static CatalogProductInput Input(string code = "PRD-001",
        CatalogItemKind kind = CatalogItemKind.StockItem) =>
        new(code, "کالای آزمایشی", kind, Guid.NewGuid());

    [Theory]
    [InlineData("PRD-01", "PRD-01")]
    [InlineData(" prd-01 ", "PRD-01")]
    [InlineData("0012", "0012")]
    public void Normalizes_customer_code_and_preserves_leading_zeroes(string code, string expected)
    {
        Assert.True(CustomerProductPolicy.TryValidate(Input(code), out var result));
        Assert.Equal(expected, result!.InternalCode);
    }

    [Fact]
    public void Product_cannot_be_silently_converted_to_service()
    {
        Assert.False(CustomerProductPolicy.TryValidate(Input("SVC", CatalogItemKind.Service), out _));
        Assert.True(CustomerProductPolicy.TryValidate(Input("A", CatalogItemKind.NonStockItem), out _));
        Assert.True(CustomerProductPolicy.CanChangeKind(CatalogItemKind.StockItem, CatalogItemKind.NonStockItem));
        Assert.False(CustomerProductPolicy.CanChangeKind(CatalogItemKind.StockItem, CatalogItemKind.Service));
        Assert.False(CustomerProductPolicy.CanChangeKind(CatalogItemKind.NonStockItem, CatalogItemKind.Service));
        Assert.True(CustomerProductPolicy.CanChangeKind(CatalogItemKind.NonStockItem, CatalogItemKind.StockItem));
        Assert.True(CustomerProductPolicy.CanChangeKind(CatalogItemKind.NonStockItem, CatalogItemKind.NonStockItem));
    }

    [Fact]
    public void Rejects_invalid_codes_and_missing_base_units()
    {
        Assert.False(CustomerProductPolicy.TryValidate(Input(" "), out _));
        Assert.False(CustomerProductPolicy.TryValidate(Input("A|B"), out _));
        Assert.False(CustomerProductPolicy.TryValidate(Input(new string('X', 101)), out _));
        Assert.False(CustomerProductPolicy.TryValidate(
            Input() with { BaseUnitId = Guid.Empty }, out _));
        Assert.False(CustomerProductPolicy.TryValidate(
            Input() with { Title = "x" }, out _));
    }

    [Fact]
    public void Stores_a_separate_thirteen_digit_official_code_and_allows_repeated_internal_codes()
    {
        var input = Input("REBAR") with { OfficialCode = "2710000113478" };
        Assert.True(CustomerProductPolicy.TryValidate(input, out var normalized));
        Assert.Equal("REBAR", normalized!.InternalCode);
        Assert.Equal("2710000113478", normalized.OfficialCode);
        Assert.False(CustomerProductPolicy.TryValidate(input with { OfficialCode = "123" }, out _));
        Assert.False(CustomerProductPolicy.TryValidate(input with { OfficialCode = "۲۷۱۰۰۰۰۱۱۳۴۷۸" }, out _));
    }

    [Fact]
    public void Discontinued_item_is_historical_and_not_editable()
    {
        Assert.True(CustomerProductPolicy.CanEdit(CatalogItemStatus.Active));
        Assert.True(CustomerProductPolicy.CanEdit(CatalogItemStatus.Inactive));
        Assert.False(CustomerProductPolicy.CanEdit(CatalogItemStatus.Discontinued));
        Assert.True(CustomerProductPolicy.IsArchived(CatalogItemStatus.Discontinued));
    }

    [Fact]
    public void Database_allows_repeated_customer_codes_and_guards_official_code_revision_and_foreign_keys()
    {
        using var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used").Options);
        var item = db.Model.FindEntityType(typeof(CustomerCatalogItem))!;
        Assert.Equal("customer_catalog_items", item.GetTableName());
        Assert.True(item.FindProperty(nameof(CustomerCatalogItem.Revision))!.IsConcurrencyToken);
        Assert.Contains(item.GetIndexes(), x => !x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(CustomerCatalogItem.CustomerUserId),
                        nameof(CustomerCatalogItem.InternalCode) }));
        Assert.Equal(13, item.FindProperty(nameof(CustomerCatalogItem.OfficialCode))!.GetMaxLength());
        Assert.All(item.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
