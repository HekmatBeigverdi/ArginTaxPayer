using ArginTaxPayer.Domain.Catalog;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Step10 / V7.9, Tables 29-32 pp50-52. Captured catalog evidence cannot
/// prove current authority directory membership without a trusted release
/// lookup; validation is draft-only and never authorizes submission.
/// </summary>
public static class OfficialV79LineCatalogRuleFactory
{
    public const string Edition = OfficialV79InvoicePatternMatrix.Edition;

    public static InvoiceRuleEngine Create() => new(Edition, new IInvoiceRule[]
    {
        new Rule("invoice.lines.presence", "V7.9 Table 29-32 pp50-52", Presence),
        new Rule("invoice.lines.catalog", "V7.9 Table 29 p50; Phase06 InvoiceCatalogHandoff V1", Catalog),
        new Rule("invoice.lines.quantity", "V7.9 Table 31 pp51-52", Quantity),
        new Rule("invoice.lines.unit", "V7.9 Table 32 p52; RC_UMGS.ST", Unit),
        new Rule("invoice.lines.pattern", "V7.9 Tables 29/31 pp50-52", Pattern)
    });

    private static IReadOnlyList<InvoiceRuleFinding> Presence(InvoiceRuleContext context)
    {
        var lines = context.CatalogLines;
        if (lines is null)
            return One("invoice.lines.presence", "invoice.lines.snapshot_missing", "body");
        if (context.Subject == InvoiceSubject.Cancellation)
            return lines.Count == 0 ? Array.Empty<InvoiceRuleFinding>() :
                One("invoice.lines.presence", "invoice.lines.cancellation_body_unverified", "body");
        if (lines.Count == 0)
            return One("invoice.lines.presence", "invoice.lines.empty", "body");
        if (context.LineIds.Count != lines.Count ||
            !context.LineIds.SequenceEqual(lines.Select(x => x.Id)) ||
            lines.Select(x => x.Id).Distinct().Count() != lines.Count ||
            lines.Select((x, i) => x.Position == i + 1).Any(valid => !valid))
            return One("invoice.lines.presence", "invoice.lines.snapshot_mismatch", "body");
        return Array.Empty<InvoiceRuleFinding>();
    }

    private static IReadOnlyList<InvoiceRuleFinding> Catalog(InvoiceRuleContext context)
    {
        if (context.CatalogLines is null) return Array.Empty<InvoiceRuleFinding>();
        var findings = new List<InvoiceRuleFinding>();
        foreach (var line in context.CatalogLines)
        {
            var s = line.Catalog;
            var path = $"body[{line.Position - 1}].sstid";
            if (s.OfficialReleaseSha256 is null || s.UnitReleaseSha256 is null ||
                !InvoiceCatalogHandoffPolicy.IsComplete(s) ||
                s.CustomerUserId != context.CustomerUserId ||
                !Enum.IsDefined(s.ItemKind) ||
                string.IsNullOrWhiteSpace(s.OfficialReleaseDocument) ||
                string.IsNullOrWhiteSpace(s.OfficialReleaseEdition) ||
                string.IsNullOrWhiteSpace(s.UnitReleaseDocument) ||
                string.IsNullOrWhiteSpace(s.UnitReleaseEdition) ||
                !HexHash(s.OfficialReleaseSha256) || !HexHash(s.UnitReleaseSha256))
                findings.Add(Finding("invoice.lines.catalog", "invoice.lines.incomplete_or_foreign_handoff", path));
            if (!Digits(s.OfficialCode, 13))
                findings.Add(Finding("invoice.lines.catalog", "invoice.lines.goods_service_code", path));
            if (!InvoiceCatalogHandoffPolicy.IsEffective(
                    s.MappingEffectiveFromUtc, s.MappingEffectiveToUtc, context.IssueDateUtc) ||
                s.EffectiveAtUtc != context.IssueDateUtc ||
                s.CapturedAtUtc > context.AsOfUtc)
                findings.Add(Finding("invoice.lines.catalog", "invoice.lines.mapping_not_effective", path));
            if (s.OfficialTitle is { Length: > 400 } || s.OfficialTitle?.Any(char.IsControl) == true)
                findings.Add(Finding("invoice.lines.catalog", "invoice.lines.title_invalid", $"body[{line.Position - 1}].sstt"));
        }
        return findings;
    }

    private static IReadOnlyList<InvoiceRuleFinding> Quantity(InvoiceRuleContext context)
    {
        if (context.CatalogLines is null) return Array.Empty<InvoiceRuleFinding>();
        var findings = new List<InvoiceRuleFinding>();
        foreach (var line in context.CatalogLines)
        {
            var quantity = line.Quantity;
            if (quantity <= 0m || decimal.Truncate(quantity) >= 1000000000000000000m ||
                quantity != decimal.Round(quantity, 8, MidpointRounding.ToZero))
                findings.Add(Finding("invoice.lines.quantity", "invoice.lines.quantity_invalid",
                    $"body[{line.Position - 1}].am"));
        }
        return findings;
    }

    private static IReadOnlyList<InvoiceRuleFinding> Unit(InvoiceRuleContext context)
    {
        if (context.CatalogLines is null) return Array.Empty<InvoiceRuleFinding>();
        var findings = new List<InvoiceRuleFinding>();
        foreach (var line in context.CatalogLines)
        {
            var code = line.Catalog.OfficialUnitCode;
            // V7.9 mu is OPTIONAL with max length 8, but the Phase06
            // contract requires a resolved official-unit reference.
            if (!Digits(code, 1, 8))
                findings.Add(Finding("invoice.lines.unit", "invoice.lines.official_unit_invalid",
                    $"body[{line.Position - 1}].mu"));
            if (line.Catalog.UnitReleaseDocument != "RC_UMGS.ST")
                findings.Add(Finding("invoice.lines.unit", "invoice.lines.unit_release_unverified",
                    $"body[{line.Position - 1}].mu"));
        }
        return findings;
    }

    private static IReadOnlyList<InvoiceRuleFinding> Pattern(InvoiceRuleContext context)
    {
        if (context.CatalogLines is null) return Array.Empty<InvoiceRuleFinding>();
        var classification = context.Classification;
        var count = context.CatalogLines.Count;
        var findings = new List<InvoiceRuleFinding>();
        if (context.Subject == InvoiceSubject.Cancellation)
            return findings;
        if (classification.Type == ElectronicInvoiceType.First && classification.Pattern is 11 or 14 &&
            count != 1)
            findings.Add(Finding("invoice.lines.pattern", "invoice.lines.single_line_required", "body"));
        if (classification.Type == ElectronicInvoiceType.First && classification.Pattern is 6 or 8)
        {
            foreach (var line in context.CatalogLines.Where(x => x.Quantity != 1m))
                findings.Add(Finding("invoice.lines.pattern", "invoice.lines.quantity_one_required",
                    $"body[{line.Position - 1}].am"));
        }
        // Exchange services, export declarations, commodity-exchange evidence,
        // chain whitelist, and VAT classification need trusted contextual data.
        return findings;
    }

    private static bool Digits(string? value, int min, int max) =>
        value is not null && value.Length >= min && value.Length <= max &&
        value.All(c => c is >= '0' and <= '9');

    private static bool Digits(string? value, int exact) => Digits(value, exact, exact);
    private static bool HexHash(string? hash) => hash is { Length: 64 } &&
        hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static InvoiceRuleFinding Finding(string key, string message, string field) =>
        new(new InvoiceRuleKey(key), InvoiceRuleSeverity.Error, message, field);

    private static IReadOnlyList<InvoiceRuleFinding> One(string key, string message, string field) =>
        new[] { Finding(key, message, field) };

    private sealed class Rule : IInvoiceRule
    {
        private readonly Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> _validate;
        public InvoiceRuleKey Key { get; }
        public InvoiceRuleSource Source { get; }

        public Rule(string key, string location,
            Func<InvoiceRuleContext, IReadOnlyList<InvoiceRuleFinding>> validate)
        {
            Key = new InvoiceRuleKey(key);
            Source = new InvoiceRuleSource("RC_IITP.IS", "V7.9", location);
            _validate = validate;
        }

        public IReadOnlyList<InvoiceRuleFinding> Evaluate(InvoiceRuleContext context) => _validate(context);
    }
}
