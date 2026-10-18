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

/// <summary>
/// V7.9 p23 mandates cutting fractional digits for IRR/foreign-currency fields.
/// Old preview API retains the edition-named type for source compatibility, but
/// caller-selected midpoint policies are no longer supported for fiscal preview.
/// </summary>
public sealed record InvoiceRoundingPolicy
{
    public string Edition { get; }
    public InvoiceRoundingPolicy(string edition)
    {
        if (string.IsNullOrWhiteSpace(edition))
            throw new ArgumentException("Policy edition is required.", nameof(edition));
        Edition = edition;
    }

    [Obsolete("V7.9 p23 requires truncation. Use the edition-only constructor.")]
    public InvoiceRoundingPolicy(string edition, MidpointRounding legacyMidpoint)
        : this(edition)
    {
        // Reject obsolete policies that previously produced rounded figures.
        // ToZero performs truncation, but it must also be source-edition pinned.
        if (legacyMidpoint != MidpointRounding.ToZero)
            throw new NotSupportedException("Midpoint rounding contradicts RC_IITP.IS V7.9 p23.");
    }

    public decimal Rial(decimal value) => decimal.Truncate(value);
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

        if (!string.Equals(rounding.Edition, OfficialV79InvoicePatternMatrix.Edition, StringComparison.Ordinal))
            throw new InvalidOperationException("Financial precision must use verified V7.9 rule edition.");

        var results = new List<InvoiceFinancialLine>();
        decimal before = 0, discount = 0, after = 0, vatSum = 0, other = 0, total = 0;
        bool complete = true;
        foreach (var x in inputs)
        {
            if (x.LineId == Guid.Empty || x.Quantity <= 0 || x.UnitPriceRial <= 0 ||
                x.DiscountRial < 0 || x.OtherTaxesRial < 0 || x.OtherLegalFeesRial < 0 ||
                x.VatRatePercent is < 0 or >= 1000 ||
                x.VatRatePercent is { } rate && decimal.Round(rate, 2) != rate)
                throw new ArgumentException("Invalid monetary inputs.");
            if (x.Quantity > 0m && (decimal.Truncate(x.Quantity) >= 1000000000000000000m ||
                x.Quantity != decimal.Round(x.Quantity, 8, MidpointRounding.ToZero)))
                throw new ArgumentException("Invalid official quantity width or precision.");
            if (decimal.Truncate(x.UnitPriceRial) >= 1000000000000000000m ||
                x.UnitPriceRial != decimal.Round(x.UnitPriceRial, 8, MidpointRounding.ToZero))
                throw new ArgumentException("Invalid official unit price width or precision.");
            var pre = rounding.Rial(checked(x.Quantity * x.UnitPriceRial));
            if (pre <= 0 || decimal.Truncate(pre).ToString(System.Globalization.CultureInfo.InvariantCulture).TrimStart('-').Length > 18 || x.DiscountRial > pre ||
                x.DiscountRial != decimal.Truncate(x.DiscountRial) ||
                x.OtherTaxesRial != decimal.Truncate(x.OtherTaxesRial) ||
                x.OtherLegalFeesRial != decimal.Truncate(x.OtherLegalFeesRial))
                throw new ArgumentException("Invalid whole-rial amounts.");
            var post = checked(pre - x.DiscountRial);
            if (post.ToString(System.Globalization.CultureInfo.InvariantCulture).Length > 18)
                throw new ArgumentException("Invalid official amount integer width.");
            decimal? vat = x.VatRatePercent is null ? null :
                rounding.Rial(checked(post * x.VatRatePercent.Value / 100m));
            if (vat is not null && vat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).Length > 18)
                throw new ArgumentException("Invalid VAT amount integer width.");
            decimal? lineTotal = vat is null ? null :
                checked(post + vat.Value + x.OtherTaxesRial + x.OtherLegalFeesRial);
            if (lineTotal is not null && lineTotal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).Length > 18)
                throw new ArgumentException("Invalid line total integer width.");
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
        if (new[] { before, discount, after, other }.Any(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture).Length > 18) ||
            complete && (vatSum.ToString(System.Globalization.CultureInfo.InvariantCulture).Length > 18 ||
                         total.ToString(System.Globalization.CultureInfo.InvariantCulture).Length > 18))
            throw new ArgumentException("Invalid invoice total integer width.");
        return new(results.AsReadOnly(), before, discount, after,
            complete ? vatSum : null, other, complete ? total : null, false);
    }
}
