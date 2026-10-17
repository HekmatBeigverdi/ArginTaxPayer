using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06SecurityRegressionTests
{
    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(100001, 100, false)]
    [InlineData(100000, 100, true)]
    [InlineData(1, 20, true)]
    public void Catalog_query_page_bounds_are_enforced(int page, int size, bool expected) =>
        Assert.Equal(expected, CatalogSearchService.ValidPage(page, size, null));

    [Fact]
    public void Official_release_never_automatically_returns_to_active()
    {
        Assert.False(CatalogReleasePolicy.CanTransition(
            CatalogReleaseState.Superseded, CatalogReleaseState.Active));
        Assert.False(CatalogReleasePolicy.CanTransition(
            CatalogReleaseState.Staged, CatalogReleaseState.Active));
        Assert.False(CatalogReleasePolicy.CanTransition(
            CatalogReleaseState.Active, CatalogReleaseState.Validated));
        Assert.True(CatalogReleasePolicy.CanTransition(
            CatalogReleaseState.Validated, CatalogReleaseState.Active));
    }

    [Fact]
    public void No_invoice_handoff_from_staged_reference_or_unreviewed_evidence()
    {
        var now = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            3, 1, 2, 2, 3, 1, 1, true, true, now, now, null));
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            3, 1, 2, 2, 3, 1, 3, false, true, now, now, null));
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            3, 1, 2, 2, 3, 1, 3, true, false, now, now, null));
    }

    [Fact]
    public void Mapping_is_effective_only_within_half_open_utc_window()
    {
        var from = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(1);
        Assert.True(InvoiceCatalogHandoffPolicy.IsEffective(from, to, from));
        Assert.False(InvoiceCatalogHandoffPolicy.IsEffective(from, to, to));
        Assert.False(InvoiceCatalogHandoffPolicy.IsEffective(
            DateTime.SpecifyKind(from, DateTimeKind.Unspecified), to, from));
    }

    [Fact]
    public void Tenant_model_uses_composite_code_uniqueness_and_restrictive_fks()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used").Options);
        var item = db.Model.FindEntityType(typeof(CustomerCatalogItem))!;
        Assert.Contains(item.GetIndexes(), idx => idx.IsUnique &&
            idx.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(CustomerCatalogItem.CustomerUserId),
                        nameof(CustomerCatalogItem.InternalCode) }));
        Assert.All(item.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

        var mapping = db.Model.FindEntityType(typeof(CatalogOfficialMapping))!;
        Assert.True(mapping.FindProperty(nameof(CatalogOfficialMapping.Revision))!.IsConcurrencyToken);
        Assert.All(mapping.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void No_catalog_role_can_nominate_level2()
    {
        Assert.False(IdentityPermissions.Grants(IdentityRoleNames.Level2, IdentityPermissions.ManageLevel2));
        Assert.False(IdentityPermissions.Grants(IdentityRoleNames.Customer, IdentityPermissions.ManageLevel2));
        Assert.True(IdentityPermissions.Grants(IdentityRoleNames.Level1, IdentityPermissions.ManageLevel2));
    }
}
