using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoiceFinancialTests
{
    private static readonly DateTime At = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);
    private static readonly InvoiceTypePattern Ordinary = new(ElectronicInvoiceType.First, 1);
    private static InvoiceRoundingPolicy Round() =>
        new(OfficialV79InvoicePatternMatrix.Edition);

    private static InvoiceAggregate Draft(Guid owner) =>
        InvoiceAggregate.CreateDraft(Guid.NewGuid(), owner, Guid.NewGuid(),
            null, At, At, new InvoicePartySnapshot(null, "فروشنده"));

    private static InvoiceCatalogHandoff Handoff(Guid owner) => new(
        1, owner, Guid.NewGuid(), 1, "A-1", "کالای داخلی",
        CatalogItemKind.StockItem, Guid.NewGuid(), "1627", "عدد",
        Guid.NewGuid(), "RC_UMGS.ST", "V1.18", new string('a', 64),
        Guid.NewGuid(), 1, At.AddDays(-1), null,
        Guid.NewGuid(), "1234567890123", "شناسه رسمی",
        Guid.NewGuid(), "RC_GS", "2026", new string('b', 64),
        At, At, InvoiceVatResolution.Unresolved);

    private static (InvoiceAggregate invoice, Guid lineId) Invoice(decimal quantity)
    {
        var owner = Guid.NewGuid();
        var doc = Draft(owner);
        var id = Guid.NewGuid();
        doc.AddDraftLine(id, quantity, Handoff(owner));
        return (doc, id);
    }

    [Fact]
    public void Ordinary_invoice_uses_decimal_math_and_header_sums()
    {
        var (doc, first) = Invoice(2.5m);
        var second = Guid.NewGuid();
        doc.AddDraftLine(second, 1m, Handoff(doc.CustomerUserId));
        var result = doc.PreviewDraftFinancials(new[]
        {
            new InvoiceFinancialInput(first, 2.5m, 100m, 25m, 10m, 4m, 6m),
            new InvoiceFinancialInput(second, 1m, 100m, 0m, 0m)
        }, Ordinary, Round());
        Assert.Equal(350m, result.BeforeDiscountRial);
        Assert.Equal(25m, result.DiscountRial);
        Assert.Equal(325m, result.AfterDiscountRial);
        Assert.Equal(22m, result.VatRial); // V7.9 p23: truncate 22.5 to 22
        Assert.Equal(10m, result.OtherTaxAndLegalFeesRial);
        Assert.Equal(357m, result.GrandTotalRial);
        Assert.False(result.IsSubmissionReady);
        Assert.Equal(InvoiceVatResolution.Unresolved, doc.Lines[0].Catalog.Vat);
    }

    [Fact]
    public void Missing_vat_does_not_mean_zero_or_a_known_total()
    {
        var (doc, id) = Invoice(1m);
        var result = doc.PreviewDraftFinancials(
            new[] { new InvoiceFinancialInput(id, 1m, 100m, 10m, null) },
            Ordinary, Round());
        Assert.Equal(90m, result.AfterDiscountRial);
        Assert.Null(result.VatRial);
        Assert.Null(result.GrandTotalRial);
        Assert.Null(Assert.Single(result.Lines).TotalRial);
        Assert.False(result.IsSubmissionReady);
    }

    [Fact]
    public void Explicit_zero_vat_is_distinct_from_unresolved()
    {
        var (doc, id) = Invoice(1m);
        var result = doc.PreviewDraftFinancials(
            new[] { new InvoiceFinancialInput(id, 1m, 100m, 0m, 0m) },
            Ordinary, Round());
        Assert.Equal(0m, result.VatRial);
        Assert.Equal(100m, result.GrandTotalRial);
        Assert.False(result.IsSubmissionReady);
    }

    [Fact]
    public void V79_fractional_midpoint_is_truncated_never_rounded_up()
    {
        var (doc, id) = Invoice(1m);
        var inputs = new[] { new InvoiceFinancialInput(id, 1m, 5m, 0m, 10m) };
        Assert.Equal(0m, doc.PreviewDraftFinancials(inputs, Ordinary, Round()).VatRial);
        Assert.Throws<NotSupportedException>(() =>
            new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition,
                MidpointRounding.AwayFromZero));
        Assert.Throws<InvalidOperationException>(() =>
            doc.PreviewDraftFinancials(inputs, Ordinary,
                new InvoiceRoundingPolicy("unverified-edition")));
    }

    [Fact]
    public void Invalid_amounts_and_unsupported_special_patterns_fail_closed()
    {
        var (doc, id) = Invoice(1m);
        Assert.Throws<ArgumentException>(() => doc.PreviewDraftFinancials(
            new[] { new InvoiceFinancialInput(id, 1m, 100m, 101m, 10m) },
            Ordinary, Round()));
        Assert.Throws<ArgumentException>(() => doc.PreviewDraftFinancials(
            new[] { new InvoiceFinancialInput(id, 1m, 100m, 0m, -1m) },
            Ordinary, Round()));
        Assert.Throws<NotSupportedException>(() => doc.PreviewDraftFinancials(
            new[] { new InvoiceFinancialInput(id, 1m, 100m, 0m, 10m) },
            new(ElectronicInvoiceType.First, 3), Round()));
    }

    [Fact]
    public void Current_line_ids_and_quantities_are_required()
    {
        var (doc, id) = Invoice(2m);
        var oldRevision = doc.Revision;
        Assert.Throws<ArgumentException>(() => doc.PreviewDraftFinancials(
            new[] { new InvoiceFinancialInput(id, 1m, 100m, 0m, 10m) },
            Ordinary, Round()));
        Assert.Throws<ArgumentException>(() => doc.PreviewDraftFinancials(
            new[] { new InvoiceFinancialInput(Guid.NewGuid(), 2m, 100m, 0m, 10m) },
            Ordinary, Round()));
        Assert.Equal(oldRevision, doc.Revision);
    }

    [Fact]
    public void Oversized_unit_price_is_rejected_before_multiplication()
    {
        var (doc, id) = Invoice(2m);
        Assert.Throws<ArgumentException>(() => doc.PreviewDraftFinancials(
            new[] { new InvoiceFinancialInput(id, 2m, decimal.MaxValue, 0m, 10m) },
            Ordinary, Round()));
    }
}
