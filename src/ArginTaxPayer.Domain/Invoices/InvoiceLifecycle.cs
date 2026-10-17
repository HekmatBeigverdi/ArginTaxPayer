namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Internal document state only. Never conflate an internally finalized invoice
/// with an accepted, registered or sent tax-authority invoice.
/// </summary>
public enum InvoiceLifecycleState
{
    Draft = 1,
    Validated = 2,
    Finalized = 3
}

/// <summary>External authority processing is tracked separately in future phases.</summary>
public enum TaxAuthorityProcessingState
{
    NotSubmitted = 1,
    Pending = 2,
    Accepted = 3,
    Rejected = 4,
    Unknown = 5
}

/// <summary>
/// Immutable, revision-pinned local validation result. Rule-set readiness and
/// tax-rate/legal coverage do not follow merely from HasErrors=false.
/// </summary>
public sealed record InvoiceValidationReceipt
{
    public Guid InvoiceId { get; }
    public long Revision { get; }
    public string RuleSetEdition { get; }
    public string RuleSetFingerprint { get; }
    public DateTime EvaluatedAtUtc { get; }
    public bool HasErrors { get; }

    public InvoiceValidationReceipt(Guid invoiceId, long revision,
        string ruleSetEdition, string ruleSetFingerprint, DateTime evaluatedAtUtc,
        bool hasErrors)
    {
        if (invoiceId == Guid.Empty || revision < 1 ||
            string.IsNullOrWhiteSpace(ruleSetEdition) ||
            !IsSha256(ruleSetFingerprint) || evaluatedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid local validation receipt.");
        InvoiceId = invoiceId;
        Revision = revision;
        RuleSetEdition = ruleSetEdition;
        RuleSetFingerprint = ruleSetFingerprint.ToLowerInvariant();
        EvaluatedAtUtc = evaluatedAtUtc;
        HasErrors = hasErrors;
    }

    internal static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}

/// <summary>
/// Finalization approval is an application-workflow assertion, not authority
/// acceptance or legal tax-rule certification; proof and permissions must be
/// checked by the application layer in future phases.
/// </summary>
public sealed record InvoiceInternalFinalizationApproval
{
    public Guid InvoiceId { get; }
    public long ValidatedRevision { get; }
    public Guid ApprovedByUserId { get; }
    public string ApprovalReference { get; }
    public DateTime ApprovedAtUtc { get; }

    public InvoiceInternalFinalizationApproval(Guid invoiceId, long validatedRevision,
        Guid approvedByUserId, string approvalReference, DateTime approvedAtUtc)
    {
        if (invoiceId == Guid.Empty || approvedByUserId == Guid.Empty ||
            validatedRevision < 1 || string.IsNullOrWhiteSpace(approvalReference) ||
            approvalReference.Length > 128 || approvalReference.Any(char.IsControl) ||
            approvedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid internal finalization approval.");
        InvoiceId = invoiceId;
        ValidatedRevision = validatedRevision;
        ApprovedByUserId = approvedByUserId;
        ApprovalReference = approvalReference;
        ApprovedAtUtc = approvedAtUtc;
    }
}

/// <summary>
/// Pure documented transition topology. Technical readiness != fiscal readiness.
/// </summary>
public static class InvoiceLifecyclePolicy
{
    public static bool CanTransition(InvoiceLifecycleState from, InvoiceLifecycleState to) =>
        from == InvoiceLifecycleState.Draft && to == InvoiceLifecycleState.Validated ||
        from == InvoiceLifecycleState.Validated && to == InvoiceLifecycleState.Draft ||
        from == InvoiceLifecycleState.Validated && to == InvoiceLifecycleState.Finalized;
}
