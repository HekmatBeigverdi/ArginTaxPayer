namespace ArginTaxPayer.Domain.Catalog;

/// <summary>Legal VAT treatment is not inferred from a numeric rate.</summary>
public enum VatTreatment
{
    Unresolved = 0,
    Standard = 1,
    Exempt = 2,
    ZeroRated = 3,
    Special = 4
}

public enum VatResolutionStatus
{
    Resolved,
    Unresolved,
    Ambiguous,
    InvalidContext
}

/// <summary>Pure, version-aware classification projection; no invoice pricing or tax posting here.</summary>
public sealed record VatRule(
    Guid RuleId,
    Guid ReleaseId,
    string ClassificationCode,
    VatTreatment Treatment,
    decimal? RatePercent,
    string LegalBasis,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool SourceVerified,
    bool ReleaseActive,
    string? ContextKey = null);

public sealed record VatResolution(
    VatResolutionStatus Status,
    VatRule? Rule = null,
    string? Reason = null);

public static class VatClassificationPolicy
{
    // RC_IITP.IS V7.9 'vra': at most three integer and two fractional digits.
    public static bool IsValidRate(decimal rate) =>
        rate >= 0m && rate <= 999.99m && decimal.Round(rate, 2) == rate;

    public static bool IsValidRule(VatRule rule)
    {
        if (rule.RuleId == Guid.Empty || rule.ReleaseId == Guid.Empty ||
            string.IsNullOrWhiteSpace(rule.ClassificationCode) ||
            string.IsNullOrWhiteSpace(rule.LegalBasis) ||
            rule.EffectiveFromUtc.Kind != DateTimeKind.Utc ||
            (rule.EffectiveToUtc is not null &&
             (rule.EffectiveToUtc.Value.Kind != DateTimeKind.Utc ||
              rule.EffectiveToUtc.Value <= rule.EffectiveFromUtc)))
            return false;

        return rule.Treatment switch
        {
            VatTreatment.Unresolved => rule.RatePercent is null,
            VatTreatment.Exempt or VatTreatment.ZeroRated => rule.RatePercent == 0m,
            VatTreatment.Standard or VatTreatment.Special =>
                rule.RatePercent is decimal rate && IsValidRate(rate),
            _ => false
        };
    }

    /// <summary>
    /// Resolve by the future invoice's legally applicable instant and optional rule context.
    /// Intervals are half-open [from,to). Multiple matching rules fail closed.
    /// A zero rate is not proof of an exemption or statutory zero-rating.
    /// </summary>
    public static VatResolution Resolve(
        IEnumerable<VatRule> rules, string? classificationCode,
        DateTime effectiveAtUtc, string? contextKey = null)
    {
        if (effectiveAtUtc.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(classificationCode))
            return new(VatResolutionStatus.InvalidContext, Reason: "invalid_effective_date_or_code");

        var eligible = rules.Where(r =>
                r.ClassificationCode == classificationCode &&
                string.Equals(r.ContextKey, contextKey, StringComparison.Ordinal) &&
                r.EffectiveFromUtc <= effectiveAtUtc &&
                (r.EffectiveToUtc is null || effectiveAtUtc < r.EffectiveToUtc.Value))
            .ToArray();
        if (eligible.Length > 1)
            return new(VatResolutionStatus.Ambiguous, Reason: "overlapping_vat_rules");
        if (eligible.Length == 0)
            return new(VatResolutionStatus.Unresolved, Reason: "no_applicable_vat_rule");

        var rule = eligible[0];
        if (!IsValidRule(rule) || !rule.SourceVerified || !rule.ReleaseActive ||
            rule.Treatment == VatTreatment.Unresolved)
            return new(VatResolutionStatus.Unresolved, Reason: "vat_rule_unverified_or_incomplete");

        return new(VatResolutionStatus.Resolved, rule);
    }
}
