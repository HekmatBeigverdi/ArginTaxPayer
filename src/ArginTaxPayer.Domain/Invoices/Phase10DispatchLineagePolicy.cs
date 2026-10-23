namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Phase10 Step15 validates trusted, server-resolved lineage before allowing a
/// candidate to enter the separate preflight/sign/dispatch pipeline.
/// This pure guard does NOT query the tax authority, reserve a DB claim, or
/// authorize sending. Officially accepted predecessor evidence must be supplied
/// by the trusted application/storage layer, never by browser-submitted fields.
/// </summary>
public sealed record InvoiceDispatchLineageContext(
    Guid InvoiceId, Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId,
    InvoiceSubjectSelection Selection, InvoiceTypePattern Classification,
    string CurrentTaxId);

public sealed record InvoiceDispatchPredecessorEvidence(
    Guid LocalInvoiceId, Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId,
    string TaxId, InvoiceSubject Subject, bool IsAuthorityAcceptanceVerified,
    bool IsCanceledOrSuperseded, InvoiceTypePattern Classification);

public enum InvoiceDispatchLineageCode
{
    EligibleCandidate = 1,
    InvalidScope = 2,
    MissingReference = 3,
    PredecessorNotResolved = 4,
    PredecessorScopeMismatch = 5,
    PredecessorNotAuthorityAccepted = 6,
    PredecessorInactive = 7,
    PredecessorTypeMismatch = 8,
    UnsupportedSubjectPattern = 9,
    ReferenceClaimConflict = 10,
    SelfReference = 11
}

public sealed record InvoiceDispatchLineageDecision(
    InvoiceDispatchLineageCode Code, string SafeMessageKey)
{
    public bool IsEligibleCandidate => Code == InvoiceDispatchLineageCode.EligibleCandidate;
    public bool IsFiscalSubmissionAuthorized => false;
}

/// <summary>
/// Claims are historical scoped reference reservations, not "one per invoice"
/// business validations. Active is a trusted durable status, NOT a browser
/// toggle. Correction/return share one slot; cancellation is separate.
/// </summary>
public sealed record InvoiceDispatchReferenceClaim(
    Guid InvoiceId, Guid CustomerUserId, Guid TaxpayerId,
    string ReferenceTaxId, InvoiceSubject Subject, bool Active);

public static class Phase10DispatchLineagePolicy
{
    public static InvoiceDispatchLineageDecision Check(
        InvoiceDispatchLineageContext current,
        InvoiceDispatchPredecessorEvidence? predecessor,
        IReadOnlyList<InvoiceDispatchReferenceClaim>? claims)
    {
        ArgumentNullException.ThrowIfNull(current);
        static InvoiceDispatchLineageDecision Result(InvoiceDispatchLineageCode code) =>
            new(code, "invoice.dispatch.lineage." + code.ToString().ToLowerInvariant());

        if (current.InvoiceId == Guid.Empty || current.CustomerUserId == Guid.Empty ||
            current.TaxpayerId == Guid.Empty || current.FiscalMemoryId == Guid.Empty ||
            current.Selection is null || current.CurrentTaxId is not { Length: 22 })
            return Result(InvoiceDispatchLineageCode.InvalidScope);
        if (!OfficialV79InvoiceSubjectPolicy.Allows(
                current.Classification, current.Selection.Subject))
            return Result(InvoiceDispatchLineageCode.UnsupportedSubjectPattern);

        if (current.Selection.Subject == InvoiceSubject.Original)
            return current.Selection.Reference is null
                ? Result(InvoiceDispatchLineageCode.EligibleCandidate)
                : Result(InvoiceDispatchLineageCode.MissingReference);

        var reference = current.Selection.Reference;
        if (reference is null)
            return Result(InvoiceDispatchLineageCode.MissingReference);
        if (string.Equals(reference.ReferenceTaxId, current.CurrentTaxId,
                StringComparison.Ordinal) ||
            reference.LocalInvoiceId == current.InvoiceId)
            return Result(InvoiceDispatchLineageCode.SelfReference);

        if (predecessor is null)
            return Result(InvoiceDispatchLineageCode.PredecessorNotResolved);
        if (predecessor.LocalInvoiceId == current.InvoiceId)
            return Result(InvoiceDispatchLineageCode.SelfReference);
        if (predecessor.LocalInvoiceId == Guid.Empty ||
            predecessor.CustomerUserId != current.CustomerUserId ||
            predecessor.TaxpayerId != current.TaxpayerId ||
            predecessor.FiscalMemoryId != current.FiscalMemoryId ||
            !string.Equals(predecessor.TaxId, reference.ReferenceTaxId,
                StringComparison.Ordinal) ||
            (reference.LocalInvoiceId is { } local &&
             predecessor.LocalInvoiceId != local))
            return Result(InvoiceDispatchLineageCode.PredecessorScopeMismatch);
        if (!predecessor.IsAuthorityAcceptanceVerified)
            return Result(InvoiceDispatchLineageCode.PredecessorNotAuthorityAccepted);
        if (predecessor.IsCanceledOrSuperseded)
            return Result(InvoiceDispatchLineageCode.PredecessorInactive);
        if (predecessor.Classification != current.Classification)
            return Result(InvoiceDispatchLineageCode.PredecessorTypeMismatch);

        // Any active, durable competing claim blocks this candidate. Do not
        // release a claim based only on timeout/HTTP error/empty inquiry.
        foreach (var claim in claims ?? Array.Empty<InvoiceDispatchReferenceClaim>())
        {
            if (claim.Active &&
                claim.InvoiceId != current.InvoiceId &&
                claim.CustomerUserId == current.CustomerUserId &&
                claim.TaxpayerId == current.TaxpayerId &&
                claim.ReferenceTaxId == reference.ReferenceTaxId &&
                (current.Selection.Subject == InvoiceSubject.Cancellation
                    ? claim.Subject == InvoiceSubject.Cancellation
                    : current.Selection.Subject is InvoiceSubject.Correction or InvoiceSubject.SalesReturn &&
                      claim.Subject is InvoiceSubject.Correction or InvoiceSubject.SalesReturn))
                return Result(InvoiceDispatchLineageCode.ReferenceClaimConflict);
        }
        return Result(InvoiceDispatchLineageCode.EligibleCandidate);
    }
}
