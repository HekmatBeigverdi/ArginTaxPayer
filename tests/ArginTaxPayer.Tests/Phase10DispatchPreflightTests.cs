using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10DispatchPreflightTests
{
    private static readonly DateTime IssueAt = new(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);

    private static (InvoiceDispatchPreflightContext Context, InvoiceWireInput Wire,
        InvoiceRuleSetRegistry Rules, InvoicePatternRegistry Patterns) Fixture(bool verified = true)
    {
        var id = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var edition = OfficialV79InvoicePatternMatrix.Edition;
        var headerHash = new string('a', 64);
        var c = new InvoiceDispatchPreflightContext(
            id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3,
            InvoiceLifecycleState.Finalized, InvoiceReviewState.Approved,
            new InvoiceTypePattern(ElectronicInvoiceType.First, 4),
            InvoiceSubjectSelection.Original(), IssueAt, edition, headerHash,
            new[] { lineId });
        var w = new InvoiceWireInput(c.Classification, InvoiceSubject.Original,
            "AAAAAAAAAAAAAAAAAAAAAA", new DateTimeOffset(IssueAt).ToUnixTimeMilliseconds(),
            "1234567890",
            new[] { new InvoiceWireLine("1234567890123", 2m, 100m,
                200, 20, 180, 10m, 18, 198, "service", "1") },
            200, 20, 180, 18, 198, ContractRegistrationNumber: "123456");

        var source = new RuleSetPrimarySource("RC_IITP.IS", edition, "verified-table",
            new string('b', 64));
        var matrix = new InvoicePatternMatrix(new InvoicePatternSource(
            "RC_IITP.IS", edition, "verified-pattern-table",
            IssueAt.AddDays(-1), null, verified),
            new[] { c.Classification });
        var ruleBundle = new InvoiceRuleBundle(OfficialV79RuleSet.Scope,
            source, new RuleSetValidity(IssueAt.AddDays(-1), null,
                dateVerified: verified, dateEvidence: verified ? source : null),
            new InvoiceRuleEngine(edition, Array.Empty<IInvoiceRule>()),
            new[] { "invoice.pattern.combination" }, coverageReviewed: verified);
        return (c, w, new InvoiceRuleSetRegistry(new[] { ruleBundle }),
            new InvoicePatternRegistry(new[] { matrix }));
    }

    [Fact]
    public void Verified_evidence_allows_immutable_candidate_but_never_authorizes_send()
    {
        var f = Fixture();
        var result = Phase10DispatchPreflight.Evaluate(f.Context, f.Wire, f.Rules, f.Patterns);
        Assert.True(result.IsPrepared);
        Assert.False(result.IsFiscalSubmissionAuthorized);
        var p = Assert.IsType<InvoiceDispatchPreparedSnapshot>(result.Snapshot);
        Assert.False(p.IsFiscalSubmissionAuthorized);
        Assert.Equal(f.Context.InvoiceId, p.InvoiceId);
        Assert.Equal(f.Context.ApprovedSnapshotSha256, p.ApprovedSnapshotSha256);
        var bytes = p.CopyWireBytes();
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            p.WirePayloadSha256);
        var original = (byte[])bytes.Clone();
        bytes[0] = 0;
        Assert.Equal(original, p.CopyWireBytes());
    }

    [Fact]
    public void Official_evidence_missing_means_no_prepared_snapshot()
    {
        var f = Fixture(false);
        var result = Phase10DispatchPreflight.Evaluate(f.Context, f.Wire, f.Rules, f.Patterns);
        Assert.False(result.IsPrepared);
        Assert.Null(result.Snapshot);
        Assert.Contains(result.Findings, x =>
            x.Code == InvoicePreflightCode.UnverifiedLegalApplicability);
        Assert.Contains(result.Findings, x =>
            x.Code == InvoicePreflightCode.IncompleteRuleCoverage);
    }

    [Fact]
    public void Draft_or_unapproved_invoice_is_denied()
    {
        var f = Fixture();
        var c = f.Context with { InvoiceState = InvoiceLifecycleState.Draft,
            ReviewState = InvoiceReviewState.InReview };
        var result = Phase10DispatchPreflight.Evaluate(c, f.Wire, f.Rules, f.Patterns);
        Assert.Null(result.Snapshot);
        Assert.Contains(result.Findings, x => x.Code == InvoicePreflightCode.InvoiceNotFinalized);
        Assert.Contains(result.Findings, x => x.Code == InvoicePreflightCode.ReviewNotApproved);
    }

    [Fact]
    public void Wrong_subject_or_date_or_missing_memory_is_denied()
    {
        var f = Fixture();
        var c = f.Context with { FiscalMemoryId = Guid.Empty,
            SubjectSelection = InvoiceSubjectSelection.Create(InvoiceSubject.Correction,
                new InvoiceReference("BBBBBBBBBBBBBBBBBBBBBB")),
            IssueDateUtc = IssueAt.AddMinutes(1) };
        var r = Phase10DispatchPreflight.Evaluate(c, f.Wire, f.Rules, f.Patterns);
        Assert.False(r.IsPrepared);
        Assert.Contains(r.Findings, x => x.Code == InvoicePreflightCode.MissingFiscalMemory);
        Assert.Contains(r.Findings, x => x.Code == InvoicePreflightCode.InvoiceSubjectMismatch);
        Assert.Contains(r.Findings, x => x.Code == InvoicePreflightCode.InvoiceDateMismatch);
    }

    [Fact]
    public void Forged_payment_or_bad_totals_are_not_prepared()
    {
        var f = Fixture();
        var bad = f.Wire with { SettlementMethod = 1 };
        var r = Phase10DispatchPreflight.Evaluate(f.Context, bad, f.Rules, f.Patterns);
        Assert.Null(r.Snapshot);
        Assert.Contains(r.Findings, x => x.Code == InvoicePreflightCode.InvalidPayload);
        var changed = f.Wire with { TotalBillRial = 199 };
        var result = Phase10DispatchPreflight.Evaluate(f.Context, changed, f.Rules, f.Patterns);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void Source_line_count_and_edition_mismatch_are_denied()
    {
        var f = Fixture();
        var c = f.Context with { InvoiceLineIds = Array.Empty<Guid>(),
            RuleSetEdition = "unrecognized" };
        var result = Phase10DispatchPreflight.Evaluate(c, f.Wire, f.Rules, f.Patterns);
        Assert.False(result.IsPrepared);
        Assert.Contains(result.Findings, x => x.Code == InvoicePreflightCode.InvalidLineSnapshot);
        Assert.Contains(result.Findings, x => x.Code == InvoicePreflightCode.RuleEditionMismatch);
    }
}
