namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Claims to validate against recalculated source-grounded amounts; none can
/// make a rate legally verified without an authoritative, date-specific
/// classification. RC_IITP.IS V7.9 pp23, 39-42, 58-64.
/// </summary>
public sealed record InvoiceClaimedLineAmounts(
    Guid LineId, decimal BeforeDiscountRial, decimal DiscountRial,
    decimal AfterDiscountRial, decimal? VatAmountRial,
    decimal OtherTaxesRial, decimal OtherLegalChargesRial,
    decimal? LineTotalRial);

public sealed record InvoiceClaimedTotals(
    decimal BeforeDiscountRial, decimal DiscountRial,
    decimal AfterDiscountRial, decimal? VatAmountRial,
    decimal OtherTaxAndChargesRial, decimal? GrandTotalRial);

/// <summary>
/// Financial validation is limited to the ordinary Type1/Pattern1 preview;
/// pattern-specific formulas and legal VAT rate attestation are not invented.
/// </summary>
public static class OfficialV79MonetaryRuleFactory
{
    public const string Edition = OfficialV79InvoicePatternMatrix.Edition;

    public static InvoiceRuleEngine Create() => new(Edition, new IInvoiceRule[]
    {
        new Rule("invoice.money.inputs", "V7.9 p23, Tables 34/40-44 pp53-63", ValidateInputs),
        new Rule("invoice.money.lines", "V7.9 Tables 40-44 pp58-63", ValidateLines),
        new Rule("invoice.money.header", "V7.9 Tables 15-20 pp39-42", ValidateTotals),
        new Rule("invoice.money.tax_resolution", "V7.9 Table 43 p61", ValidateTaxResolution)
    });

    private static IReadOnlyList<InvoiceRuleFinding> ValidateInputs(InvoiceRuleContext c)
    {
        var result = new List<InvoiceRuleFinding>();
        if (c.MonetaryInputs is null || c.MonetaryInputs.Count == 0)
            return One("invoice.money.inputs", "invoice.money.inputs_missing", "body");
        if (c.Subject == InvoiceSubject.Cancellation ||
            c.Classification != new InvoiceTypePattern(ElectronicInvoiceType.First, 1))
            return One("invoice.money.inputs", "invoice.money.special_pattern_unimplemented", "header.inp");
        if (c.LineIds.Count != c.MonetaryInputs.Count ||
            c.LineIds.Distinct().Count() != c.MonetaryInputs.Count ||
            !c.LineIds.SequenceEqual(c.MonetaryInputs.Select(x => x.LineId)) ||
            c.MonetaryInputs.Any(x => x.LineId == Guid.Empty))
            result.Add(Finding("invoice.money.inputs", "invoice.money.line_identity_mismatch", "body"));
        if (c.CatalogLines is null || c.CatalogLines.Count != c.MonetaryInputs.Count ||
            !c.CatalogLines.Select(x => (x.Id, x.Quantity))
                .SequenceEqual(c.MonetaryInputs.Select(x => (x.LineId, x.Quantity))))
            result.Add(Finding("invoice.money.inputs", "invoice.money.catalog_quantity_mismatch", "body"));
        foreach (var item in c.MonetaryInputs.Select((value, index) => (value, index)))
        {
            var i = item.value;
            if (i.Quantity <= 0m ||
                i.UnitPriceRial <= 0m ||
                i.UnitPriceRial != decimal.Round(i.UnitPriceRial, 8, MidpointRounding.ToZero) ||
                i.VatRatePercent is < 0m or > 999.99m ||
                i.VatRatePercent is { } vat && vat != decimal.Round(vat, 2, MidpointRounding.ToZero) ||
                i.DiscountRial < 0m || i.OtherTaxesRial < 0m || i.OtherLegalFeesRial < 0m ||
                i.DiscountRial != decimal.Truncate(i.DiscountRial) ||
                i.OtherTaxesRial != decimal.Truncate(i.OtherTaxesRial) ||
                i.OtherLegalFeesRial != decimal.Truncate(i.OtherLegalFeesRial))
                result.Add(Finding("invoice.money.inputs", "invoice.money.input_precision_invalid",
                    $"body[{item.index}]"));
        }
        // An invalid discount after truncation, excessive resulting amount,
        // or checked decimal overflow must become an explicit error, not an
        // empty set of findings when downstream comparisons are skipped.
        try
        {
            InvoiceFinancialCalculator.Preview(c.MonetaryInputs, c.Classification,
                new InvoiceRoundingPolicy(Edition));
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            result.Add(Finding("invoice.money.inputs", "invoice.money.calculation_invalid", "body"));
        }
        return result;
    }

    private static IReadOnlyList<InvoiceRuleFinding> ValidateLines(InvoiceRuleContext c)
    {
        if (!TryCalculate(c, out var calculated)) return Array.Empty<InvoiceRuleFinding>();
        var result = new List<InvoiceRuleFinding>();
        if (c.ClaimedAmounts is null || c.ClaimedAmounts.Count != calculated!.Lines.Count)
            return One("invoice.money.lines", "invoice.money.claims_missing", "body");
        for (var index = 0; index < calculated.Lines.Count; index++)
        {
            var expected = calculated.Lines[index];
            var claimed = c.ClaimedAmounts[index];
            if (claimed.LineId != expected.LineId ||
                claimed.BeforeDiscountRial != expected.BeforeDiscountRial ||
                claimed.DiscountRial != expected.DiscountRial ||
                claimed.AfterDiscountRial != expected.AfterDiscountRial ||
                claimed.VatAmountRial != expected.VatRial ||
                claimed.OtherTaxesRial != expected.OtherTaxesRial ||
                claimed.OtherLegalChargesRial != expected.OtherLegalFeesRial ||
                claimed.LineTotalRial != expected.TotalRial)
                result.Add(Finding("invoice.money.lines", "invoice.money.line_mismatch", $"body[{index}]"));
        }
        return result;
    }

    private static IReadOnlyList<InvoiceRuleFinding> ValidateTotals(InvoiceRuleContext c)
    {
        if (!TryCalculate(c, out var calculated)) return Array.Empty<InvoiceRuleFinding>();
        if (c.ClaimedTotals is null)
            return One("invoice.money.header", "invoice.money.header_missing", "header");
        var expected = calculated!;
        var claimed = c.ClaimedTotals;
        var result = new List<InvoiceRuleFinding>();
        if (expected.BeforeDiscountRial != claimed.BeforeDiscountRial)
            result.Add(Finding("invoice.money.header", "invoice.money.tprdis_mismatch", "header.tprdis"));
        if (expected.DiscountRial != claimed.DiscountRial)
            result.Add(Finding("invoice.money.header", "invoice.money.tdis_mismatch", "header.tdis"));
        if (expected.AfterDiscountRial != claimed.AfterDiscountRial)
            result.Add(Finding("invoice.money.header", "invoice.money.tadis_mismatch", "header.tadis"));
        if (expected.VatRial != claimed.VatAmountRial)
            result.Add(Finding("invoice.money.header", "invoice.money.tvam_mismatch", "header.tvam"));
        if (expected.OtherTaxAndLegalFeesRial != claimed.OtherTaxAndChargesRial)
            result.Add(Finding("invoice.money.header", "invoice.money.todam_mismatch", "header.todam"));
        if (expected.GrandTotalRial != claimed.GrandTotalRial)
            result.Add(Finding("invoice.money.header", "invoice.money.tbill_mismatch", "header.tbill"));
        return result;
    }

    private static IReadOnlyList<InvoiceRuleFinding> ValidateTaxResolution(InvoiceRuleContext c)
    {
        if (c.MonetaryInputs is null) return Array.Empty<InvoiceRuleFinding>();
        var result = new List<InvoiceRuleFinding>();
        foreach (var item in c.MonetaryInputs.Select((value, index) => (value, index)))
        {
            if (item.value.VatRatePercent is null)
                result.Add(Finding("invoice.money.tax_resolution", "invoice.money.vat_unresolved",
                    $"body[{item.index}].vra"));
        }
        return result;
    }

    private static bool TryCalculate(InvoiceRuleContext c, out InvoiceFinancialPreview? preview)
    {
        preview = null;
        if (c.MonetaryInputs is null || c.MonetaryInputs.Count == 0 ||
            c.Classification != new InvoiceTypePattern(ElectronicInvoiceType.First, 1) ||
            c.Subject == InvoiceSubject.Cancellation) return false;
        try
        {
            preview = InvoiceFinancialCalculator.Preview(c.MonetaryInputs,
                c.Classification, new InvoiceRoundingPolicy(Edition));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            // Invalid inputs are reported by inputs rule. Never fabricate
            // monetary comparisons from an invalid or overflowed calculation.
            return false;
        }
    }

    private static InvoiceRuleFinding Finding(string key, string message, string field) =>
        new(new InvoiceRuleKey(key), InvoiceRuleSeverity.Error, message, field);
    private static IReadOnlyList<InvoiceRuleFinding> One(string key, string message, string field) =>
        new[] { Finding(key, message, field) };

    private sealed class Rule : IInvoiceRule
    {
        private readonly Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> _evaluate;
        public InvoiceRuleKey Key { get; }
        public InvoiceRuleSource Source { get; }
        public Rule(string key, string citation,
            Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> evaluate)
        {
            Key = new InvoiceRuleKey(key);
            Source = new InvoiceRuleSource("RC_IITP.IS", "V7.9", citation);
            _evaluate = evaluate;
        }
        public IReadOnlyList<InvoiceRuleFinding> Evaluate(InvoiceRuleContext context) => _evaluate(context);
    }
}
