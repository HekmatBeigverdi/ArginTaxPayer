using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07RuleSetVersioningTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string Scope = OfficialV79RuleSet.Scope;

    private static RuleSetPrimarySource Source(string edition) =>
        new("RC_IITP.IS", edition, "Table 9 p31",
            new string('a', 64));

    private static InvoiceRuleBundle Bundle(string edition, DateTime start,
        DateTime? end, bool verified = false, bool covered = false)
    {
        var evidence = verified
            ? new RuleSetPrimarySource("OFFICIAL-EFFECTIVE-NOTICE", edition,
                "effective date section", new string('b', 64))
            : null;
        return new InvoiceRuleBundle(Scope, Source(edition),
            new RuleSetValidity(start, end, verified, evidence),
            new InvoiceRuleEngine(edition, Array.Empty<IInvoiceRule>()),
            covered ? new[] { "invoice.header.issue_date" } : Array.Empty<string>(),
            coverageReviewed: covered);
    }

    private static InvoiceRuleContext Context(DateTime date, string edition) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, date,
            new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            InvoiceSubject.Original, Array.Empty<Guid>(), edition);

    [Fact]
    public void An_explicit_provisional_window_never_certifies_the_tax_authority()
    {
        var engine = new InvoiceRuleEngine(
            OfficialV79InvoicePatternMatrix.Edition, Array.Empty<IInvoiceRule>());
        var bundle = OfficialV79RuleSet.Draft(engine, Start, Start.AddMonths(1));
        var registry = new InvoiceRuleSetRegistry(new[] { bundle });
        var selected = registry.Resolve(Scope, bundle.Edition, Start);
        Assert.Equal(RuleSetLookupStatus.SelectedProvisional, selected.Status);
        Assert.False(selected.IsSubmissionAuthorized);
        Assert.False(bundle.Validity.DateVerified);
        Assert.Equal(
            "fb333770cddafd92d4ed3a5db0a21c1b0c47305431598f297f70fe5dd4c416be",
            bundle.Source.Sha256);
        Assert.Equal(RuleSetLookupStatus.UnverifiedDate,
            registry.CheckEvidenceReadiness(Scope, Context(Start, bundle.Edition)).Status);
        Assert.False(registry.ReplayDraft(Scope, Context(Start, bundle.Edition)).IsSubmissionAuthorized);
    }

    [Fact]
    public void Half_open_intervals_select_exact_boundaries_without_ambiguity()
    {
        var next = Start.AddDays(10);
        var registry = new InvoiceRuleSetRegistry(new[]
        {
            Bundle("v1", Start, next),
            Bundle("v2", next, next.AddDays(10))
        });
        Assert.Equal(RuleSetLookupStatus.SelectedProvisional,
            registry.Resolve(Scope, "v1", Start).Status);
        Assert.Equal(RuleSetLookupStatus.OutsideWindow,
            registry.Resolve(Scope, "v1", next).Status);
        Assert.Equal(RuleSetLookupStatus.SelectedProvisional,
            registry.Resolve(Scope, "v2", next).Status);
        Assert.Throws<InvalidOperationException>(() =>
            registry.ReplayDraft(Scope, Context(next, "v1")));
    }

    [Fact]
    public void Overlapping_windows_for_same_scope_are_rejected_even_for_different_editions()
    {
        Assert.Throws<ArgumentException>(() => new InvoiceRuleSetRegistry(new[]
        {
            Bundle("v1", Start, Start.AddDays(10)),
            Bundle("v2", Start.AddDays(9), Start.AddDays(20))
        }));
    }

    [Fact]
    public void Duplicate_edition_or_missing_edition_is_never_fallback()
    {
        Assert.Throws<ArgumentException>(() =>
            new InvoiceRuleSetRegistry(new[]
            {
                Bundle("v1", Start, Start.AddDays(10)),
                Bundle("v1", Start, Start.AddDays(10))
            }));
        var registry = new InvoiceRuleSetRegistry(new[]
        {
            Bundle("v1", Start, Start.AddDays(10))
        });
        Assert.Equal(RuleSetLookupStatus.EditionNotFound,
            registry.Resolve(Scope, "latest", Start).Status);
    }

    [Fact]
    public void Validity_rejects_local_dates_and_claimed_verification_without_evidence()
    {
        Assert.Throws<ArgumentException>(() =>
            new RuleSetValidity(DateTime.SpecifyKind(Start, DateTimeKind.Local), null));
        Assert.Throws<ArgumentException>(() =>
            new RuleSetValidity(Start, Start));
        Assert.Throws<ArgumentException>(() =>
            new RuleSetValidity(Start, null, dateVerified: true));
        Assert.Throws<ArgumentException>(() =>
            new RuleSetValidity(Start, null, dateEvidence: Source("v1")));
    }

    [Fact]
    public void Claimed_verified_date_and_coverage_are_still_not_send_permission()
    {
        var registry = new InvoiceRuleSetRegistry(new[]
        {
            Bundle("v1", Start, Start.AddDays(5), verified: true, covered: true)
        });
        var ctx = Context(Start.AddDays(1), "v1");
        var status = registry.CheckEvidenceReadiness(Scope, ctx);
        Assert.Equal(RuleSetLookupStatus.SelectedWithEvidence, status.Status);
        Assert.False(status.IsSubmissionAuthorized);
        Assert.False(registry.ReplayDraft(Scope, ctx).IsSubmissionAuthorized);
    }

    [Fact]
    public void Missing_coverage_blocks_evidence_readiness()
    {
        var registry = new InvoiceRuleSetRegistry(new[]
        {
            Bundle("v1", Start, Start.AddDays(5), verified: true)
        });
        Assert.Equal(RuleSetLookupStatus.IncompleteCoverage,
            registry.CheckEvidenceReadiness(Scope, Context(Start, "v1")).Status);
    }

    [Fact]
    public void Registry_copies_input_list_and_prevents_mutation()
    {
        var list = new List<InvoiceRuleBundle> { Bundle("v1", Start, Start.AddDays(3)) };
        var registry = new InvoiceRuleSetRegistry(list);
        list.Clear();
        Assert.Single(registry.Bundles);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<InvoiceRuleBundle>)registry.Bundles).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)registry.Bundles[0].CoveredRuleKeys).Add("invoice.header.a"));
    }

    [Fact]
    public void Source_fingerprint_and_covered_rule_keys_are_validated()
    {
        Assert.Throws<ArgumentException>(() =>
            new RuleSetPrimarySource("RC_IITP.IS", "v1", "p31", "bad"));
        Assert.Throws<ArgumentException>(() =>
            new InvoiceRuleBundle(Scope, Source("v1"),
                new RuleSetValidity(Start, null),
                new InvoiceRuleEngine("v1", Array.Empty<IInvoiceRule>()),
                new[] { "invoice..bad" }));
        Assert.Throws<ArgumentException>(() =>
            new InvoiceRuleBundle(Scope, Source("v1"),
                new RuleSetValidity(Start, null),
                new InvoiceRuleEngine("v2", Array.Empty<IInvoiceRule>()),
                Array.Empty<string>()));
    }
}
