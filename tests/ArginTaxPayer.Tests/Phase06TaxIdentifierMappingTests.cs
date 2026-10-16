using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06TaxIdentifierMappingTests
{
    private static readonly DateTime Start = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);
    private static TaxIdentifierMappingWindow Window(int beginDays, int? endDays = null) =>
        new(Start.AddDays(beginDays), endDays is null ? null : Start.AddDays(endDays.Value));

    [Fact]
    public void Half_open_adjacent_windows_do_not_overlap()
    {
        Assert.True(TaxIdentifierMappingPolicy.IsValidWindow(Window(0, 10)));
        Assert.False(TaxIdentifierMappingPolicy.Overlaps(Window(0, 10), Window(10, 20)));
        Assert.True(TaxIdentifierMappingPolicy.Overlaps(Window(0, 10), Window(9, 20)));
        Assert.True(TaxIdentifierMappingPolicy.Overlaps(Window(0), Window(10, 20)));
    }

    [Fact]
    public void Rejects_invalid_temporal_windows()
    {
        Assert.False(TaxIdentifierMappingPolicy.IsValidWindow(Window(0, 0)));
        Assert.False(TaxIdentifierMappingPolicy.IsValidWindow(Window(10, 0)));
        Assert.False(TaxIdentifierMappingPolicy.IsValidWindow(
            new TaxIdentifierMappingWindow(DateTime.SpecifyKind(Start, DateTimeKind.Local), null)));
        Assert.False(TaxIdentifierMappingPolicy.CanClose(Window(0), Start));
        Assert.True(TaxIdentifierMappingPolicy.CanClose(Window(0), Start.AddDays(1)));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, true)]
    [InlineData(3, 2, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, false)]
    [InlineData(3, 1, false)]
    [InlineData(7, 1, false)]
    public void Item_kind_must_match_official_goods_service_kind(int kind, int officialKind, bool allowed)
    {
        Assert.Equal(allowed, TaxIdentifierMappingPolicy.CanMap(
            kind, officialKind, 2, 3, true, true));
    }

    [Fact]
    public void Mapping_fails_closed_without_active_verified_audited_reference()
    {
        Assert.False(TaxIdentifierMappingPolicy.CanMap(1, 1, 1, 3, true, true));
        Assert.False(TaxIdentifierMappingPolicy.CanMap(1, 1, 2, 1, true, true));
        Assert.False(TaxIdentifierMappingPolicy.CanMap(1, 1, 2, 4, true, true));
        Assert.False(TaxIdentifierMappingPolicy.CanMap(1, 1, 2, 3, false, true));
        Assert.False(TaxIdentifierMappingPolicy.CanMap(1, 1, 2, 3, true, false));
    }

    [Fact]
    public void Ef_mapping_has_immutable_history_foreign_keys_and_revision()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used")
                .Options);
        var entity = db.Model.FindEntityType(typeof(CatalogOfficialMapping))!;
        Assert.Equal("catalog_official_mappings", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(CatalogOfficialMapping.Revision))!.IsConcurrencyToken);
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(CatalogOfficialMapping.ItemId), nameof(CatalogOfficialMapping.EffectiveFromUtc) }));
    }
}
