namespace ArginTaxPayer.Domain.Invoices;

// Draft-only preview of ordinary Type 1/Pattern 1 invoices.
// Primary authority: RC_IITP.IS V7.9 tables 15-20, 40-44.
// Not an authority acceptance or a rule-version verification.
public sealed record InvoiceFinancialInput(
    Guid LineId, decimal Quantity, decimal UnitPriceRial,
    decimal DiscountRial, decimal? VatRatePercent,
    decimal OtherTaxesRial = 0m, decimal OtherLegalFeesRial = 0m);

public sealed record InvoiceFinancialLine(
    Guid LineId, decimal BeforeDiscountRial, decimal DiscountRial,
    decimal AfterDiscountRial, decimal? VatRial, decimal OtherTaxesRial,
    decimal OtherLegalFeesRial, decimal? TotalRial);

public sealed record InvoiceFinancialPreview(
    IReadOnlyList<InvoiceFinancialLine> Lines, decimal BeforeDiscountRial,
    decimal DiscountRial, decimal AfterDiscountRial, decimal? VatRial,
    decimal OtherTaxAndLegalFeesRial, decimal? GrandTotalRial,
    bool IsSubmissionReady = false);

public sealed record InvoiceRoundingPolicy(string Edition, MidpointRounding Midpoint)
{
    public decimal Rial(decimal value)
    {
        if (string.IsNullOrWhiteSpace(Edition) || !Enum.IsDefined(Midpoint))
            throw new ArgumentException("Explicit rounding policy required.");
        return decimal.Round(value, 0, Midpoint);
    }
}

public static class InvoiceFinancialCalculator
{
    public static InvoiceFinancialPreview Preview(
        IReadOnlyList<InvoiceFinancialInput> inputs, InvoiceTypePattern typePattern,
        InvoiceRoundingPolicy rounding)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(rounding);
        if (typePattern != new InvoiceTypePattern(ElectronicInvoiceType.First, 1))
            throw new NotSupportedException("Special type/pattern formula not implemented.");
        if (inputs.Count == 0)
            throw new ArgumentException("At least one line required.");

        var results = new List<InvoiceFinancialLine>();
        decimal before = 0, discount = 0, after = 0, vatSum = 0, other = 0, total = 0;
        bool complete = true;
        foreach (var x in inputs)
        {
            if (x.LineId == Guid.Empty || x.Quantity <= 0 || x.UnitPriceRial <= 0 ||
                x.DiscountRial < 0 || x.OtherTaxesRial < 0 || x.OtherLegalFeesRial < 0 ||
                x.VatRatePercent is < 0 or > 999 ||
                x.VatRatePercent is { } rate && decimal.Round(rate, 2) != rate)
                throw new ArgumentException("Invalid monetary inputs.");
            var pre = rounding.Rial(checked(x.Quantity * x.UnitPriceRial));
            if (pre <= 0 || x.DiscountRial > pre ||
                x.DiscountRial != decimal.Truncate(x.DiscountRial) ||
                x.OtherTaxesRial != decimal.Truncate(x.OtherTaxesRial) ||
                x.OtherLegalFeesRial != decimal.Truncate(x.OtherLegalFeesRial))
                throw new ArgumentException("Invalid whole-rial amounts.");
            var post = checked(pre - x.DiscountRial);
            decimal? vat = x.VatRatePercent is null ? null :
                rounding.Rial(checked(post * x.VatRatePercent.Value / 100m));
            decimal? lineTotal = vat is null ? null :
                checked(post + vat.Value + x.OtherTaxesRial + x.OtherLegalFeesRial);
            before = checked(before + pre);
            discount = checked(discount + x.DiscountRial);
            after = checked(after + post);
            other = checked(other + x.OtherTaxesRial + x.OtherLegalFeesRial);
            if (vat is null) complete = false;
            else
            {
                vatSum = checked(vatSum + vat.Value);
                total = checked(total + lineTotal!.Value);
            }
            results.Add(new(x.LineId, pre, x.DiscountRial, post, vat,
                x.OtherTaxesRial, x.OtherLegalFeesRial, lineTotal));
        }
        return new(results.AsReadOnly(), before, discount, after,
            complete ? vatSum : null, other, complete ? total : null, false);
    }
}
