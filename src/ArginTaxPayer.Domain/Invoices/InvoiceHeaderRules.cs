namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Immutable projected official header fields for validation. All source values
/// are caller-supplied and must be reconciled with registered taxpayer data.
/// RC_IITP.IS V7.9 pp24-25 and pp32-35.
/// </summary>
public sealed record InvoiceHeaderFields(
    string? TaxId, long? IssuedUnixMilliseconds, string? SellerEconomicId,
    int? BuyerPersonType = null, string? BuyerEconomicId = null,
    string? BuyerIdentityId = null, string? BuyerPostalCode = null,
    string? SellerBranchCode = null, string? BuyerBranchCode = null);

/// <summary>
/// Explicit as-of instant, not wall-clock access in the rule. TaxId checksum,
/// memory linkage, fiscal registry consistency, deadlines and predecessor
/// chronology require external verified inputs and are outside Step08.
/// </summary>
public static class InvoiceHeaderRuleFactory
{
    public const string Edition = OfficialV79InvoicePatternMatrix.Edition;

    public static InvoiceRuleEngine Create() => new(Edition,
        new IInvoiceRule[]
        {
            new HeaderRule("invoice.header.taxid", "Table 3 p24", ValidateTaxId),
            new HeaderRule("invoice.header.issue_date", "Table 4 p25", ValidateIssueDate),
            new HeaderRule("invoice.header.seller", "Table 11 pp33-34", ValidateSeller),
            new HeaderRule("invoice.header.buyer", "Table 10 p32, Table 11 pp33-35", ValidateBuyer)
        });

    private static IReadOnlyList<InvoiceRuleFinding> ValidateTaxId(InvoiceRuleContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.Header?.TaxId))
            return One("invoice.header.taxid", "invoice.header.taxid.required", "header.taxid");
        if (ctx.Header.TaxId.Length != 22 ||
            !ctx.Header.TaxId.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            return One("invoice.header.taxid", "invoice.header.taxid.invalid_structure", "header.taxid");
        return Array.Empty<InvoiceRuleFinding>();
    }

    private static IReadOnlyList<InvoiceRuleFinding> ValidateIssueDate(InvoiceRuleContext ctx)
    {
        var result = new List<InvoiceRuleFinding>();
        if (ctx.Header?.IssuedUnixMilliseconds is not long millis)
            result.Add(Finding("invoice.header.issue_date", "invoice.header.issue_date.required", "header.indatim"));
        else
        {
            try
            {
                var utc = DateTimeOffset.FromUnixTimeMilliseconds(millis);
                if (millis < 0 || millis.ToString(System.Globalization.CultureInfo.InvariantCulture).Length > 13 ||
                    utc.UtcDateTime != ctx.IssueDateUtc)
                    result.Add(Finding("invoice.header.issue_date", "invoice.header.issue_date.invalid", "header.indatim"));
                if (utc > ctx.AsOfUtc)
                    result.Add(Finding("invoice.header.issue_date", "invoice.header.issue_date.future", "header.indatim"));
            }
            catch (ArgumentOutOfRangeException)
            {
                result.Add(Finding("invoice.header.issue_date", "invoice.header.issue_date.invalid", "header.indatim"));
            }
        }
        return result;
    }

    private static IReadOnlyList<InvoiceRuleFinding> ValidateSeller(InvoiceRuleContext ctx)
    {
        var result = new List<InvoiceRuleFinding>();
        if (!Digits(ctx.Header?.SellerEconomicId, 11, 14))
            result.Add(Finding("invoice.header.seller", "invoice.header.seller.economic_id", "header.tins"));
        if (ctx.Header?.SellerBranchCode is { } branch && !Digits(branch, 4))
            result.Add(Finding("invoice.header.seller", "invoice.header.seller.branch", "header.sbc"));
        return result;
    }

    private static IReadOnlyList<InvoiceRuleFinding> ValidateBuyer(InvoiceRuleContext ctx)
    {
        var h = ctx.Header;
        var result = new List<InvoiceRuleFinding>();
        if (h is null) return One("invoice.header.buyer", "invoice.header.buyer.header_missing", "header");
        var referencing = ctx.Subject != InvoiceSubject.Original;
        if (referencing)
        {
            if (h.BuyerPersonType is not null || h.BuyerEconomicId is not null ||
                h.BuyerIdentityId is not null || h.BuyerPostalCode is not null ||
                h.BuyerBranchCode is not null)
                result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.outside_pattern", "header"));
            return result;
        }

        // For Type 2/3 and Type1 export/commodity-exchange, buyer data is optional
        // per Table11 p34; if supplied, validate shapes below.
        var optional = ctx.Classification.Type != ElectronicInvoiceType.First ||
            ctx.Classification.Pattern is 7 or 11;
        if (h.BuyerPersonType is { } buyerType && buyerType is < 1 or > 4)
            result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.person_type", "header.tob"));
        if (h.BuyerEconomicId is { } econ && !Digits(econ, 11, 14))
            result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.economic_id", "header.tinb"));
        if (h.BuyerIdentityId is { } identity &&
            !(Digits(identity, 10) || Digits(identity, 11) || Digits(identity, 12)))
            result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.identity", "header.bid"));
        if (h.BuyerPostalCode is { } postal && !Digits(postal, 10))
            result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.postal_code", "header.bpc"));
        if (h.BuyerBranchCode is { } branch && !Digits(branch, 4))
            result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.branch", "header.bbc"));

        if (!optional)
        {
            if (h.BuyerPersonType is null)
                result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.person_type_required", "header.tob"));
            else if (h.BuyerPersonType is 1 or 4 &&
                     string.IsNullOrWhiteSpace(h.BuyerEconomicId) &&
                     (string.IsNullOrWhiteSpace(h.BuyerIdentityId) ||
                      string.IsNullOrWhiteSpace(h.BuyerPostalCode)))
                result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.natural_identity_required", "header"));
            else if (h.BuyerPersonType is 2 or 3 && string.IsNullOrWhiteSpace(h.BuyerEconomicId))
                result.Add(Finding("invoice.header.buyer", "invoice.header.buyer.economic_required", "header.tinb"));
        }
        return result;
    }

    private static bool Digits(string? value, params int[] lengths) =>
        value is not null && lengths.Contains(value.Length) &&
        value.All(c => c is >= '0' and <= '9');

    private static InvoiceRuleFinding Finding(string key, string message, string path) =>
        new(new InvoiceRuleKey(key), InvoiceRuleSeverity.Error, message, path);

    private static IReadOnlyList<InvoiceRuleFinding> One(string key, string message, string path) =>
        new[] { Finding(key, message, path) };

    private sealed class HeaderRule : IInvoiceRule
    {
        private readonly Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> _validate;
        public InvoiceRuleKey Key { get; }
        public InvoiceRuleSource Source { get; }

        public HeaderRule(string key, string location,
            Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> validate)
        {
            Key = new InvoiceRuleKey(key);
            Source = new InvoiceRuleSource("RC_IITP.IS", "V7.9", location);
            _validate = validate;
        }

        public IReadOnlyList<InvoiceRuleFinding> Evaluate(InvoiceRuleContext context) => _validate(context);
    }
}
