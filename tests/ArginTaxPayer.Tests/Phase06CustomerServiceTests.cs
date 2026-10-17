using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06CustomerServiceTests
{
    private static CatalogServiceInput Input(string code = "SVC-001") =>
        new(code, "خدمات نصب", Guid.NewGuid());

    [Theory]
    [InlineData("svc-001", "SVC-001")]
    [InlineData(" SVC-001 ", "SVC-001")]
    [InlineData("00125", "00125")]
    public void Service_internal_code_is_customer_owned_and_canonical(string raw, string expected)
    {
        Assert.True(CustomerServicePolicy.TryValidate(Input(raw), out var value));
        Assert.Equal(expected, value!.InternalCode);
        Assert.Equal("خدمات نصب", value.Title);
    }

    [Fact]
    public void Service_kind_cannot_be_interchanged_with_goods()
    {
        Assert.True(CustomerServicePolicy.IsService(CatalogItemKind.Service));
        Assert.False(CustomerServicePolicy.IsService(CatalogItemKind.StockItem));
        Assert.False(CustomerServicePolicy.IsService(CatalogItemKind.NonStockItem));
        Assert.False(CustomerProductPolicy.TryValidate(
            new CatalogProductInput("SVC", "خدمات نصب", CatalogItemKind.Service, Guid.NewGuid()),
            out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab|cd")]
    [InlineData("with\nnewline")]
    public void Invalid_service_codes_are_rejected(string code)
    {
        Assert.False(CustomerServicePolicy.TryValidate(Input(code), out _));
    }

    [Fact]
    public void Invalid_title_and_unit_are_rejected()
    {
        Assert.False(CustomerServicePolicy.TryValidate(
            Input() with { Title = "x" }, out _));
        Assert.False(CustomerServicePolicy.TryValidate(
            Input() with { BaseUnitId = Guid.Empty }, out _));
        Assert.False(CustomerServicePolicy.TryValidate(
            Input(new string('x', 101)), out _));
    }

    [Fact]
    public void Discontinued_service_is_historically_retained_but_not_mutable()
    {
        Assert.True(CustomerServicePolicy.CanEdit(CatalogItemStatus.Active));
        Assert.True(CustomerServicePolicy.CanEdit(CatalogItemStatus.Inactive));
        Assert.False(CustomerServicePolicy.CanEdit(CatalogItemStatus.Blocked));
        Assert.False(CustomerServicePolicy.CanEdit(CatalogItemStatus.Discontinued));
    }

    [Fact]
    public void Service_shares_customer_unique_code_namespace_and_restrictive_foreign_keys()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used").Options);
        var model = db.Model.FindEntityType(typeof(CustomerCatalogItem))!;
        Assert.Equal("customer_catalog_items", model.GetTableName());
        Assert.Contains(model.GetIndexes(), idx => idx.IsUnique &&
            idx.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(CustomerCatalogItem.CustomerUserId),
                        nameof(CustomerCatalogItem.InternalCode) }));
        Assert.True(model.FindProperty(nameof(CustomerCatalogItem.Revision))!.IsConcurrencyToken);
        Assert.All(model.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
