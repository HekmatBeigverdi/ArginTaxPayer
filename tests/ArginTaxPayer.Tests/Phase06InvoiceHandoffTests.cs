using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06InvoiceHandoffTests
{
    private static readonly DateTime IssuedAt = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, 10, 10, false)]
    [InlineData(0, 10, 9, true)]
    [InlineData(0, 10, 0, true)]
    [InlineData(0, -1, 10, false)]
    public void Effective_date_uses_half_open_window(
        int startOffset, int endOffset, int issueOffset, bool valid)
    {
        var start = IssuedAt.AddDays(startOffset);
        var end = IssuedAt.AddDays(endOffset);
        var asOf = IssuedAt.AddDays(issueOffset);
        Assert.Equal(valid, InvoiceCatalogHandoffPolicy.IsEffective(start, end, asOf));
    }

    [Fact]
    public void Effective_dates_require_utc()
    {
        Assert.False(InvoiceCatalogHandoffPolicy.IsEffective(
            DateTime.SpecifyKind(IssuedAt, DateTimeKind.Unspecified), null, IssuedAt));
        Assert.False(InvoiceCatalogHandoffPolicy.IsEffective(
            IssuedAt, null, DateTime.SpecifyKind(IssuedAt, DateTimeKind.Local)));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, true)]
    [InlineData(3, 2, true)]
    [InlineData(1, 2, false)]
    [InlineData(3, 1, false)]
    public void Goods_and_service_types_remain_separate(int itemKind, int officialKind, bool valid)
    {
        Assert.Equal(valid, InvoiceCatalogHandoffPolicy.CanCapture(
            itemKind, 1, officialKind, 2, 3, 1, 3,
            reviewedSource: true, unitReviewed: true, IssuedAt, IssuedAt, null));
    }

    [Fact]
    public void Unreviewed_unactivated_staged_units_and_inactive_items_are_rejected()
    {
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            1, 1, 1, 2, 3, 1, 1, true, true, IssuedAt, IssuedAt, null));
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            1, 1, 1, 2, 3, 1, 3, true, false, IssuedAt, IssuedAt, null));
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            1, 1, 1, 2, 3, 1, 3, false, true, IssuedAt, IssuedAt, null));
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            1, 4, 1, 2, 3, 1, 3, true, true, IssuedAt, IssuedAt, null));
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            1, 1, 1, 1, 3, 1, 3, true, true, IssuedAt, IssuedAt, null));
        Assert.False(InvoiceCatalogHandoffPolicy.CanCapture(
            1, 1, 1, 2, 4, 1, 3, true, true, IssuedAt, IssuedAt, null));
    }

    [Fact]
    public void Snapshot_contract_has_version_provenance_and_unresolved_vat()
    {
        var snapshot = new InvoiceCatalogHandoff(
            1, Guid.NewGuid(), Guid.NewGuid(), 1, "SKU-001", "کاغذ A4",
            CatalogItemKind.StockItem,
            Guid.NewGuid(), "1627", "عدد",
            Guid.NewGuid(), "RC_UMGS.ST", "V1.18", new string('a', 64),
            Guid.NewGuid(), 1, IssuedAt, null,
            Guid.NewGuid(), "1234567890123", "کاغذ A4 رسمی",
            Guid.NewGuid(), "RC_GS", "2026", new string('b', 64),
            IssuedAt, IssuedAt, InvoiceVatResolution.Unresolved);
        Assert.True(InvoiceCatalogHandoffPolicy.IsComplete(snapshot));
        Assert.Equal(1, snapshot.ContractVersion);
        Assert.Equal(InvoiceVatResolution.Unresolved, snapshot.Vat);
        Assert.Equal("1234567890123", snapshot.OfficialCode);
        Assert.Equal("1627", snapshot.OfficialUnitCode);
        Assert.Equal(64, snapshot.OfficialReleaseSha256.Length);
        Assert.False(InvoiceCatalogHandoffPolicy.IsComplete(snapshot with { OfficialReleaseSha256 = "" }));
        Assert.False(InvoiceCatalogHandoffPolicy.IsComplete(snapshot with { ContractVersion = 2 }));
    }

    [Fact]
    public void Handoff_does_not_mutate_or_issue_invoice()
    {
        var methods = typeof(InvoiceCatalogHandoffService).GetMethods()
            .Where(x => x.DeclaringType == typeof(InvoiceCatalogHandoffService))
            .Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "CaptureAsync" }, methods);
        Assert.DoesNotContain(typeof(InvoiceCatalogHandoff).GetProperties(),
            p => p.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Contains("InvoiceNumber", StringComparison.OrdinalIgnoreCase));
    }
}
