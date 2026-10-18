namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Frozen rule bundle. "Coverage reviewed" is an assertion supplied by
/// authorized governance; it cannot prove complete legislation or submission.
/// </summary>
public sealed class InvoiceRuleBundle
{
    public string Scope { get; }
    public string Edition { get; }
    public RuleSetPrimarySource Source { get; }
    public RuleSetValidity Validity { get; }
    public InvoiceRuleEngine Engine { get; }
    public IReadOnlyList<string> CoveredRuleKeys { get; }
    public bool CoverageReviewed { get; }

    public InvoiceRuleBundle(string scope, RuleSetPrimarySource source,
        RuleSetValidity validity, InvoiceRuleEngine engine,
        IEnumerable<string> coveredRuleKeys, bool coverageReviewed = false)
    {
        if (string.IsNullOrWhiteSpace(scope) || source is null || validity is null ||
            engine is null || source.Edition != engine.Edition || coveredRuleKeys is null)
            throw new ArgumentException("Matching source and rule edition required.");

        var list = coveredRuleKeys.ToArray();
        if (list.Any(x => string.IsNullOrWhiteSpace(x) || x != new InvoiceRuleKey(x).Value) ||
            list.Distinct(StringComparer.Ordinal).Count() != list.Length)
            throw new ArgumentException("Invalid or duplicate stable rule keys.");
        Scope = scope;
        Edition = source.Edition;
        Source = source;
        Validity = validity;
        Engine = engine;
        CoveredRuleKeys = Array.AsReadOnly(list.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        CoverageReviewed = coverageReviewed;
    }
}

public sealed record RuleSetLookup(RuleSetLookupStatus Status, InvoiceRuleBundle? Bundle)
{
    public bool IsSubmissionAuthorized => false;
}

/// <summary>
/// No automatic latest-version fallback; replay pins edition and issue UTC.
/// Same-scope time windows cannot overlap, including across editions.
/// </summary>
public sealed class InvoiceRuleSetRegistry
{
    public IReadOnlyList<InvoiceRuleBundle> Bundles { get; }

    public InvoiceRuleSetRegistry(IEnumerable<InvoiceRuleBundle> bundles)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        var entries = bundles.ToArray();
        if (entries.Any(x => x is null) ||
            entries.GroupBy(x => (x.Scope, x.Edition)).Any(g => g.Count() != 1))
            throw new ArgumentException("Duplicate or null rule bundle.");
        foreach (var group in entries.GroupBy(x => x.Scope, StringComparer.Ordinal))
        {
            var sorted = group.OrderBy(x => x.Validity.StartUtc).ToArray();
            for (var i = 1; i < sorted.Length; i++)
                if (sorted[i - 1].Validity.Overlaps(sorted[i].Validity))
                    throw new ArgumentException("Overlapping effective rule-set windows.");
        }
        Bundles = Array.AsReadOnly(entries.OrderBy(x => x.Scope, StringComparer.Ordinal)
            .ThenBy(x => x.Validity.StartUtc).ToArray());
    }

    public RuleSetLookup Resolve(string scope, string edition,
        DateTime issueUtc, bool requireVerifiedDate = false)
    {
        if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(edition) ||
            issueUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Explicit scope, edition and UTC date required.");

        var entry = Bundles.FirstOrDefault(x => x.Scope == scope && x.Edition == edition);
        if (entry is null) return new(RuleSetLookupStatus.EditionNotFound, null);
        if (!entry.Validity.Contains(issueUtc))
            return new(RuleSetLookupStatus.OutsideWindow, null);
        if (requireVerifiedDate && !entry.Validity.DateVerified)
            return new(RuleSetLookupStatus.UnverifiedDate, null);
        return new(entry.Validity.DateVerified
            ? RuleSetLookupStatus.SelectedWithEvidence
            : RuleSetLookupStatus.SelectedProvisional, entry);
    }

    public InvoiceRuleEvaluation ReplayDraft(string scope, InvoiceRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var found = Resolve(scope, context.RuleSetEdition, context.IssueDateUtc);
        if (found.Bundle is null)
            throw new InvalidOperationException("Requested historical edition is unavailable.");
        return found.Bundle.Engine.Evaluate(context);
    }

    public RuleSetLookup CheckEvidenceReadiness(string scope, InvoiceRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var found = Resolve(scope, context.RuleSetEdition, context.IssueDateUtc,
            requireVerifiedDate: true);
        if (found.Bundle is null) return found;
        if (!found.Bundle.CoverageReviewed || found.Bundle.CoveredRuleKeys.Count == 0)
            return new(RuleSetLookupStatus.IncompleteCoverage, null);
        // No version registry may authorize actual invoice transmission.
        return found;
    }
}

public static class OfficialV79RuleSet
{
    public const string Scope = "iran.taxpayer.einvoice";

    public static InvoiceRuleBundle Draft(
        InvoiceRuleEngine engine, DateTime fromUtc, DateTime? toUtc = null,
        IEnumerable<string>? keys = null) =>
        new(Scope, new RuleSetPrimarySource(
            "RC_IITP.IS", OfficialV79InvoicePatternMatrix.Edition,
            "Tir 1405; Tables 1, 9, 10, 29-44, pp14,31-32,50-63",
            "fb333770cddafd92d4ed3a5db0a21c1b0c47305431598f297f70fe5dd4c416be"),
            new RuleSetValidity(fromUtc, toUtc), engine,
            keys ?? Array.Empty<string>());
}
