namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Payload field presence, distinct from internal draft classification. Null means
/// omitted from outbound JSON. V7.9 Table 10 p32: cancellation omits inty/inp.
/// Derived identifiers never authorize an external fiscal submission.
/// </summary>
public sealed record InvoicePatternHeaderFields(
    int? TransmittedType, int? TransmittedPattern,
    string? ReferenceTaxId = null, string? ContractNumber = null,
    string? ServiceBillId = null, int? FlightType = null,
    decimal? GoldConstructionFeeRial = null,
    decimal? GoldSellerProfitRial = null,
    decimal? GoldCommissionRial = null,
    decimal? GoldTotalFeeProfitRial = null);

/// <summary>
/// Step09 partial V7.9 type/pattern/subject and conditional header rules.
/// Technical schema checks for pattern-specific body fields, industry eligibility
/// and effective-dated legal authorizations are deferred to Steps10-12.
/// </summary>
public static class OfficialV79TypePatternRuleFactory
{
    public const string Edition = OfficialV79InvoicePatternMatrix.Edition;
    private static readonly InvoicePatternMatrix Matrix = OfficialV79InvoicePatternMatrix.Create();

    public static InvoiceRuleEngine Create() => new(Edition, new IInvoiceRule[]
    {
        new Rule("invoice.pattern.combination", "Table 9 p31", Combination),
        new Rule("invoice.pattern.subject", "Table 10 p32", Subject),
        new Rule("invoice.pattern.payload", "Table 10 p32 and Table 8 p30", Payload),
        new Rule("invoice.pattern.conditional", "Table 9 p31; Table 11 pp33-35; Table 14 p38", Conditional)
    });

    private static IReadOnlyList<InvoiceRuleFinding> Combination(InvoiceRuleContext c) =>
        Matrix.Evaluate(c.Classification, c.IssueDateUtc) == InvoicePatternDecision.AllowedByReferencedEdition
            ? Array.Empty<InvoiceRuleFinding>()
            : One("invoice.pattern.combination", "invoice.pattern.unsupported", "header.inp");

    private static IReadOnlyList<InvoiceRuleFinding> Subject(InvoiceRuleContext c) =>
        OfficialV79InvoiceSubjectPolicy.Allows(c.Classification, c.Subject)
            ? Array.Empty<InvoiceRuleFinding>()
            : One("invoice.pattern.subject", "invoice.pattern.subject_disallowed", "header.ins");

    private static IReadOnlyList<InvoiceRuleFinding> Payload(InvoiceRuleContext c)
    {
        var p = c.PatternHeader;
        if (p is null)
            return One("invoice.pattern.payload", "invoice.pattern.payload_required", "header");
        var r = new List<InvoiceRuleFinding>();
        if (c.Subject == InvoiceSubject.Cancellation)
        {
            if (p.TransmittedType is not null || p.TransmittedPattern is not null)
                r.Add(Finding("invoice.pattern.payload", "invoice.pattern.cancellation_outside_pattern", "header"));
        }
        else if (p.TransmittedType != (int)c.Classification.Type ||
                 p.TransmittedPattern != c.Classification.Pattern)
            r.Add(Finding("invoice.pattern.payload", "invoice.pattern.type_pattern_mismatch", "header"));

        if (c.Subject == InvoiceSubject.Original)
        {
            if (p.ReferenceTaxId is not null)
                r.Add(Finding("invoice.pattern.payload", "invoice.pattern.reference_outside_pattern", "header.irtaxid"));
        }
        else if (!TaxIdShape(p.ReferenceTaxId))
            r.Add(Finding("invoice.pattern.payload", "invoice.pattern.reference_required", "header.irtaxid"));
        return r;
    }

    private static IReadOnlyList<InvoiceRuleFinding> Conditional(InvoiceRuleContext c)
    {
        var p = c.PatternHeader;
        if (p is null) return One("invoice.pattern.conditional", "invoice.pattern.payload_required", "header");
        var results = new List<InvoiceRuleFinding>();
        var first = c.Classification.Type == ElectronicInvoiceType.First;
        var pattern = c.Classification.Pattern;
        if (first && c.Subject == InvoiceSubject.Original && pattern == 4 &&
            !Digits(p.ContractNumber, 1, 12))
            results.Add(Finding("invoice.pattern.conditional",
                "invoice.pattern.contract_required", "header.crn"));
        if (first && c.Subject == InvoiceSubject.Original && pattern == 5 &&
            !Digits(p.ServiceBillId, 1, 19))
            results.Add(Finding("invoice.pattern.conditional",
                "invoice.pattern.bill_required", "header.billid"));
        if (first && c.Subject == InvoiceSubject.Original && pattern == 6 &&
            p.FlightType is not (1 or 2))
            results.Add(Finding("invoice.pattern.conditional",
                "invoice.pattern.flight_required", "header.ft"));
        if (pattern == 3 && (c.Classification.Type is ElectronicInvoiceType.First or ElectronicInvoiceType.Second) &&
            c.Subject != InvoiceSubject.Cancellation &&
            (p.GoldConstructionFeeRial is null || p.GoldSellerProfitRial is null ||
             p.GoldCommissionRial is null || p.GoldTotalFeeProfitRial is null))
            results.Add(Finding("invoice.pattern.conditional",
                "invoice.pattern.gold_fields_required", "body"));
        // These fields are not general-purpose fields; prevent sending them
        // on unrelated original invoice patterns without inventing defaults.
        if (c.Subject == InvoiceSubject.Original && !(first && pattern == 4) &&
            p.ContractNumber is not null)
            results.Add(Finding("invoice.pattern.conditional",
                "invoice.pattern.contract_outside_pattern", "header.crn"));
        if (c.Subject == InvoiceSubject.Original && !(first && pattern == 5) &&
            p.ServiceBillId is not null)
            results.Add(Finding("invoice.pattern.conditional",
                "invoice.pattern.bill_outside_pattern", "header.billid"));
        if (c.Subject == InvoiceSubject.Original && !(first && pattern == 6) &&
            p.FlightType is not null)
            results.Add(Finding("invoice.pattern.conditional",
                "invoice.pattern.flight_outside_pattern", "header.ft"));
        return results;
    }

    private static bool TaxIdShape(string? value) =>
        value is { Length: 22 } &&
        value.All(ch => ch is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    private static bool Digits(string? value, int minimum, int maximum) =>
        value is not null && value.Length >= minimum && value.Length <= maximum &&
        value.All(ch => ch is >= '0' and <= '9');

    private static InvoiceRuleFinding Finding(string key, string message, string field) =>
        new(new InvoiceRuleKey(key), InvoiceRuleSeverity.Error, message, field);

    private static IReadOnlyList<InvoiceRuleFinding> One(string key, string message, string field) =>
        new[] { Finding(key, message, field) };

    private sealed class Rule : IInvoiceRule
    {
        private readonly Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> _check;
        public InvoiceRuleKey Key { get; }
        public InvoiceRuleSource Source { get; }

        public Rule(string key, string location,
            Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> check)
        {
            Key = new InvoiceRuleKey(key);
            Source = new InvoiceRuleSource("RC_IITP.IS", "V7.9", location);
            _check = check;
        }

        public IReadOnlyList<InvoiceRuleFinding> Evaluate(InvoiceRuleContext context) => _check(context);
    }
}
