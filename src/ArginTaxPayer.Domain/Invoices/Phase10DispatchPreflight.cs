using System.Security.Cryptography;
using System.Text;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Immutable proposal generated from already authorized server-side data.
/// Not a fiscal signing permit: source provenance, tenant authorization,
/// active fiscal memory, key custody and effective official validation must
/// be verified independently at the application boundary.
/// </summary>
public sealed record InvoiceDispatchPreflightContext(
    Guid InvoiceId, Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId,
    long InvoiceRevision, InvoiceLifecycleState InvoiceState,
    InvoiceReviewState ReviewState, InvoiceTypePattern Classification,
    InvoiceSubjectSelection SubjectSelection, DateTime IssueDateUtc,
    string RuleSetEdition, string ApprovedSnapshotSha256,
    IReadOnlyList<Guid> InvoiceLineIds);

public enum InvoicePreflightCode
{
    InvalidContext = 1,
    InvoiceNotFinalized = 2,
    ReviewNotApproved = 3,
    RuleEditionMismatch = 4,
    UnverifiedLegalApplicability = 5,
    IncompleteRuleCoverage = 6,
    InvoiceTypeMismatch = 7,
    InvoiceSubjectMismatch = 8,
    InvoiceDateMismatch = 9,
    MissingFiscalMemory = 10,
    InvalidLineSnapshot = 11,
    InvalidReference = 12,
    InvalidPayload = 13
}

public sealed record InvoiceDispatchPreflightFinding(InvoicePreflightCode Code, string MessageKey);

public sealed class InvoiceDispatchPreparedSnapshot
{
    private readonly byte[] _wireBytes;
    public Guid InvoiceId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public Guid FiscalMemoryId { get; }
    public long InvoiceRevision { get; }
    public string ApprovedSnapshotSha256 { get; }
    public string WirePayloadSha256 { get; }
    public string RuleSetEdition { get; }
    public string MapperVersion { get; }
    public InvoiceTypePattern Classification { get; }
    public InvoiceSubject Subject { get; }
    public bool IsFiscalSubmissionAuthorized => false;

    internal InvoiceDispatchPreparedSnapshot(InvoiceDispatchPreflightContext context,
        InvoiceWireProjection wire)
    {
        InvoiceId = context.InvoiceId;
        CustomerUserId = context.CustomerUserId;
        TaxpayerId = context.TaxpayerId;
        FiscalMemoryId = context.FiscalMemoryId;
        InvoiceRevision = context.InvoiceRevision;
        ApprovedSnapshotSha256 = context.ApprovedSnapshotSha256.ToLowerInvariant();
        RuleSetEdition = wire.RuleSetEdition;
        MapperVersion = wire.MapperVersion;
        Classification = wire.Classification;
        Subject = wire.Subject;
        _wireBytes = Encoding.UTF8.GetBytes(wire.Json);
        WirePayloadSha256 = Convert.ToHexString(SHA256.HashData(_wireBytes)).ToLowerInvariant();
    }

    /// <summary>
    /// Defensive copy; no mutable internal byte array is ever handed to callers.
    /// Only a separately authorized application signing service may consume it.
    /// </summary>
    public byte[] CopyWireBytes() => (byte[])_wireBytes.Clone();
}

/// <summary>
/// A preparation result is never dispatch authorization. Validation is fail-closed:
/// incomplete official evidence cannot be overridden by the absence of findings.
/// </summary>
public sealed record InvoiceDispatchPreflightResult(
    InvoiceDispatchPreparedSnapshot? Snapshot,
    IReadOnlyList<InvoiceDispatchPreflightFinding> Findings)
{
    public bool IsPrepared => Snapshot is not null && Findings.Count == 0;
    public bool IsFiscalSubmissionAuthorized => false;
}

public static class Phase10DispatchPreflight
{
    public static InvoiceDispatchPreflightResult Evaluate(
        InvoiceDispatchPreflightContext context,
        InvoiceWireInput input,
        InvoiceRuleSetRegistry ruleSets,
        InvoicePatternRegistry patterns)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ruleSets);
        ArgumentNullException.ThrowIfNull(patterns);

        var findings = new List<InvoiceDispatchPreflightFinding>();
        void Add(InvoicePreflightCode code) =>
            findings.Add(new(code, "invoice.preflight." + ToSnakeCase(code)));

        if (context.InvoiceId == Guid.Empty || context.CustomerUserId == Guid.Empty ||
            context.TaxpayerId == Guid.Empty || context.FiscalMemoryId == Guid.Empty ||
            context.InvoiceRevision < 1 || context.IssueDateUtc.Kind != DateTimeKind.Utc ||
            !InvoiceValidationReceipt.IsSha256(context.ApprovedSnapshotSha256) ||
            context.InvoiceLineIds is null ||
            context.InvoiceLineIds.Any(x => x == Guid.Empty) ||
            context.InvoiceLineIds.Distinct().Count() != context.InvoiceLineIds.Count)
            Add(InvoicePreflightCode.InvalidContext);

        if (context.FiscalMemoryId == Guid.Empty)
            Add(InvoicePreflightCode.MissingFiscalMemory);
        if (context.InvoiceState != InvoiceLifecycleState.Finalized)
            Add(InvoicePreflightCode.InvoiceNotFinalized);
        if (context.ReviewState != InvoiceReviewState.Approved)
            Add(InvoicePreflightCode.ReviewNotApproved);
        if (!string.Equals(context.RuleSetEdition, OfficialV79InvoicePatternMatrix.Edition, StringComparison.Ordinal))
            Add(InvoicePreflightCode.RuleEditionMismatch);
        if (context.Classification != input.Classification)
            Add(InvoicePreflightCode.InvoiceTypeMismatch);
        if (context.SubjectSelection is null ||
            context.SubjectSelection.Subject != input.Subject ||
            !string.Equals(context.SubjectSelection.Reference?.ReferenceTaxId,
                input.ReferenceTaxId, StringComparison.Ordinal))
            Add(InvoicePreflightCode.InvoiceSubjectMismatch);

        DateTimeOffset issue;
        try { issue = DateTimeOffset.FromUnixTimeMilliseconds(input.IssueUnixMilliseconds); }
        catch (ArgumentOutOfRangeException)
        {
            issue = DateTimeOffset.MinValue;
            Add(InvoicePreflightCode.InvoiceDateMismatch);
        }
        if (context.IssueDateUtc.Kind != DateTimeKind.Utc ||
            issue.UtcDateTime != context.IssueDateUtc)
            if (!findings.Any(x => x.Code == InvoicePreflightCode.InvoiceDateMismatch))
                Add(InvoicePreflightCode.InvoiceDateMismatch);

        if (context.InvoiceLineIds is null ||
            (input.Subject != InvoiceSubject.Cancellation &&
             (input.Lines is null || input.Lines.Count != context.InvoiceLineIds.Count)))
            Add(InvoicePreflightCode.InvalidLineSnapshot);

        if (input.Subject != InvoiceSubject.Original &&
            context.SubjectSelection?.Reference is null)
            Add(InvoicePreflightCode.InvalidReference);

        // Official pattern catalogue explicitly contains unverified rows.
        // A pattern being listed in an old edition is NOT a live issuance grant.
        if (context.IssueDateUtc.Kind != DateTimeKind.Utc ||
            patterns.Evaluate(context.RuleSetEdition, context.Classification,
                context.IssueDateUtc, requireSubmissionVerified: true)
                != InvoicePatternDecision.AllowedByReferencedEdition)
            Add(InvoicePreflightCode.UnverifiedLegalApplicability);

        // Registry provenance and effective-dated coverage are separate from
        // the type/pattern catalogue. Both must be independently reviewed.
        // These flags allow local snapshot preparation, NOT fiscal dispatch.
        if (context.IssueDateUtc.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(context.RuleSetEdition))
            Add(InvoicePreflightCode.IncompleteRuleCoverage);
        else
        {
            var rule = ruleSets.Resolve(OfficialV79RuleSet.Scope,
                context.RuleSetEdition, context.IssueDateUtc, requireVerifiedDate: true);
            if (rule.Status != RuleSetLookupStatus.SelectedWithEvidence ||
                rule.Bundle is null || !rule.Bundle.CoverageReviewed ||
                rule.Bundle.CoveredRuleKeys.Count == 0)
                Add(InvoicePreflightCode.IncompleteRuleCoverage);
        }

        if (findings.Count > 0)
            return new(null, Array.AsReadOnly(findings.DistinctBy(x => x.Code).ToArray()));

        try
        {
            var wire = Phase10InvoiceWireMapper.Project(input);
            // Defensive: the prepared object cannot represent a different edition.
            if (wire.Classification != context.Classification ||
                wire.Subject != context.SubjectSelection.Subject ||
                wire.RuleSetEdition != context.RuleSetEdition)
                return new(null, Array.AsReadOnly(new[] {
                    new InvoiceDispatchPreflightFinding(InvoicePreflightCode.InvalidPayload,
                        "invoice.preflight.invalid_payload") }));
            return new(new InvoiceDispatchPreparedSnapshot(context, wire),
                Array.Empty<InvoiceDispatchPreflightFinding>());
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            // No raw exception text leaves this pure boundary.
            return new(null, Array.AsReadOnly(new[] {
                new InvoiceDispatchPreflightFinding(InvoicePreflightCode.InvalidPayload,
                    "invoice.preflight.invalid_payload") }));
        }
    }

    private static string ToSnakeCase(InvoicePreflightCode code)
    {
        var value = code.ToString();
        var b = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            if (char.IsUpper(c) && b.Length > 0) b.Append('_');
            b.Append(char.ToLowerInvariant(c));
        }
        return b.ToString();
    }
}
