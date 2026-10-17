using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07MonetaryTaxRulesTests
{
    private static readonly DateTime Time = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);
    private static readonly InvoiceTypePattern Ordinary = new(ElectronicInvoiceType.First, 1);

    private static InvoiceCatalogHandoff Handoff(Guid owner) => new(
        1, owner, Guid.NewGuid(), 1, "A-1", "کالا",
        CatalogItemKind.StockItem, Guid.NewGuid(), "1627", "عدد",
        Guid.NewGuid(), "RC_UMGS.ST", "V1.18", new string('a', 64),
        Guid.NewGuid(), 1, Time.AddDays(-1), null,
        Guid.NewGuid(), "1234567890123", "رسمی",
        Guid.NewGuid(), "RC_GS", "2026", new string('b', 64),
        Time, Time, InvoiceVatResolution.Unresolved);

    private static (InvoiceRuleContext context, InvoiceFinancialPreview preview) Build(
        decimal quantity = 1m, decimal price = 5m, decimal discount = 0m,
        decimal? vatRate = 10m, decimal otherTaxes = 0m,
        decimal otherLegal = 0m, InvoiceClaimedTotals? overriddenHeader = null,
        InvoiceClaimedLineAmounts? overriddenLine = null)
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var line = InvoiceLineSnapshot.Create(id, 1, quantity, Handoff(owner), owner);
        var input = new InvoiceFinancialInput(id, quantity, price, discount, vatRate, otherTaxes, otherLegal);
        var preview = InvoiceFinancialCalculator.Preview(new[] { input }, Ordinary,
            new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition));
        var expected = Assert.Single(preview.Lines);
        var claimed = overriddenLine ?? new InvoiceClaimedLineAmounts(
            id, expected.BeforeDiscountRial, expected.DiscountRial, expected.AfterDiscountRial,
            expected.VatRial, expected.OtherTaxesRial, expected.OtherLegalFeesRial,
            expected.TotalRial);
        var header = overriddenHeader ?? new InvoiceClaimedTotals(
            preview.BeforeDiscountRial, preview.DiscountRial, preview.AfterDiscountRial,
            preview.VatRial, preview.OtherTaxAndLegalFeesRial, preview.GrandTotalRial);
        var ctx = new InvoiceRuleContext(
            Guid.NewGuid(), owner, Guid.NewGuid(), 1, Time, Ordinary,
            InvoiceSubject.Original, new[] { id }, OfficialV79MonetaryRuleFactory.Edition,
            catalogLines: new[] { line }, monetaryInputs: new[] { input },
            claimedAmounts: new[] { claimed }, claimedTotals: header);
        return (ctx, preview);
    }

    [Fact]
    public void Decimal_truncation_is_used_for_prdis_and_vam()
    {
        var (ctx, expected) = Build(quantity: 1.1m, price: 5m, vatRate: 10m);
        Assert.Equal(5m, expected.BeforeDiscountRial); // 5.5 cuts to 5, not 6
        Assert.Equal(0m, expected.VatRial);            // .5 cuts to 0, not 1
        Assert.Empty(OfficialV79MonetaryRuleFactory.Create().Evaluate(ctx).Findings);
    }

    [Fact]
    public void Official_header_claim_mismatch_is_reported_per_field()
    {
        var (reference, baseline) = Build(quantity: 2m, price: 100m, discount: 20m,
            vatRate: 10m, otherTaxes: 3m, otherLegal: 2m);
        var incorrect = new InvoiceClaimedTotals(
            baseline.BeforeDiscountRial, 21m, baseline.AfterDiscountRial,
            baseline.VatRial, baseline.OtherTaxAndLegalFeesRial, baseline.GrandTotalRial + 1m);
        var (context, _) = Build(quantity: 2m, price: 100m, discount: 20m,
            vatRate: 10m, otherTaxes: 3m, otherLegal: 2m, overriddenHeader: incorrect);
        var findings = OfficialV79MonetaryRuleFactory.Create().Evaluate(context).Findings;
        Assert.Contains(findings, f => f.FieldPath == "header.tdis");
        Assert.Contains(findings, f => f.FieldPath == "header.tbill");
    }

    [Fact]
    public void Official_line_amount_mismatch_is_rejected()
    {
        var (baseline, preview) = Build();
        var original = Assert.Single(preview.Lines);
        var wrong = new InvoiceClaimedLineAmounts(original.LineId,
            original.BeforeDiscountRial + 1m, original.DiscountRial,
            original.AfterDiscountRial, original.VatRial,
            original.OtherTaxesRial, original.OtherLegalFeesRial, original.TotalRial);
        var context = new InvoiceRuleContext(
            baseline.InvoiceId, baseline.CustomerUserId, baseline.TaxpayerId, baseline.Revision,
            baseline.IssueDateUtc, baseline.Classification, baseline.Subject,
            baseline.LineIds, baseline.RuleSetEdition,
            catalogLines: baseline.CatalogLines, monetaryInputs: baseline.MonetaryInputs,
            claimedAmounts: new[] { wrong }, claimedTotals: baseline.ClaimedTotals);
        Assert.Contains(OfficialV79MonetaryRuleFactory.Create().Evaluate(context).Findings,
            f => f.MessageKey == "invoice.money.line_mismatch");
    }

    [Fact]
    public void Vat_unresolved_produces_error_and_never_becomes_zero()
    {
        var (ctx, p) = Build(vatRate: null);
        Assert.Null(p.VatRial);
        Assert.Null(p.GrandTotalRial);
        var result = OfficialV79MonetaryRuleFactory.Create().Evaluate(ctx);
        Assert.Contains(result.Findings, f => f.MessageKey == "invoice.money.vat_unresolved");
        Assert.False(result.IsSubmissionAuthorized);
    }

    [Fact]
    public void Explicit_zero_rate_is_numeric_only_not_tax_authority_approval()
    {
        var (ctx, p) = Build(vatRate: 0m);
        Assert.Equal(0m, p.VatRial);
        var result = OfficialV79MonetaryRuleFactory.Create().Evaluate(ctx);
        Assert.Empty(result.Findings);
        Assert.False(result.IsSubmissionAuthorized);
    }

    [Fact]
    public void Amounts_and_tax_are_rejected_when_precision_is_invalid()
    {
        var (baseline, _) = Build();
        var badInput = new InvoiceFinancialInput(baseline.LineIds[0],
            1m, 1.123456789m, 0m, 10.125m);
        var ctx = new InvoiceRuleContext(baseline.InvoiceId, baseline.CustomerUserId,
            baseline.TaxpayerId, baseline.Revision, Time, Ordinary, InvoiceSubject.Original,
            baseline.LineIds, baseline.RuleSetEdition, catalogLines: baseline.CatalogLines,
            monetaryInputs: new[] { badInput });
        Assert.Contains(OfficialV79MonetaryRuleFactory.Create().Evaluate(ctx).Findings,
            f => f.MessageKey == "invoice.money.input_precision_invalid");
    }

    [Fact]
    public void Wrong_catalog_quantity_fails_even_when_claims_are_valid()
    {
        var (baseline, _) = Build(quantity: 2m);
        var wrongInput = new InvoiceFinancialInput(baseline.LineIds[0], 1m, 5m, 0m, 10m);
        var ctx = new InvoiceRuleContext(baseline.InvoiceId, baseline.CustomerUserId,
            baseline.TaxpayerId, baseline.Revision, Time, Ordinary, InvoiceSubject.Original,
            baseline.LineIds, baseline.RuleSetEdition, catalogLines: baseline.CatalogLines,
            monetaryInputs: new[] { wrongInput }, claimedAmounts: baseline.ClaimedAmounts,
            claimedTotals: baseline.ClaimedTotals);
        Assert.Contains(OfficialV79MonetaryRuleFactory.Create().Evaluate(ctx).Findings,
            f => f.MessageKey == "invoice.money.catalog_quantity_mismatch");
    }

    [Fact]
    public void Unsupported_gold_exchange_or_export_is_not_marked_valid()
    {
        var (baseline, _) = Build();
        foreach (var pattern in new[] { 2, 3, 7 })
        {
            var ctx = new InvoiceRuleContext(baseline.InvoiceId, baseline.CustomerUserId,
                baseline.TaxpayerId, baseline.Revision, Time,
                new InvoiceTypePattern(ElectronicInvoiceType.First, pattern), InvoiceSubject.Original,
                baseline.LineIds, baseline.RuleSetEdition, catalogLines: baseline.CatalogLines,
                monetaryInputs: baseline.MonetaryInputs, claimedAmounts: baseline.ClaimedAmounts,
                claimedTotals: baseline.ClaimedTotals);
            var outcome = OfficialV79MonetaryRuleFactory.Create().Evaluate(ctx);
            Assert.Contains(outcome.Findings,
                f => f.MessageKey == "invoice.money.special_pattern_unimplemented");
            Assert.False(outcome.IsSubmissionAuthorized);
        }
    }

    [Fact]
    public void Rule_evaluation_is_pure_and_repeatable()
    {
        var (ctx, _) = Build();
        var engine = OfficialV79MonetaryRuleFactory.Create();
        var a = engine.Evaluate(ctx);
        var b = engine.Evaluate(ctx);
        Assert.Equal(a.Findings, b.Findings);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<InvoiceFinancialInput>)ctx.MonetaryInputs!).Clear());
    }
}
