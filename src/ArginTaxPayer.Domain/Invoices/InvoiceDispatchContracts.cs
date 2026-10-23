namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Phase10 submission-attempt lifecycle. Independent of internal invoice review
/// and TaxAuthorityProcessingState. Pure policy: NOT a sender, repository or authority assertion.
/// </summary>
public enum InvoiceDispatchState
{
    Prepared = 1,
    Signed = 2,
    Reserved = 3,
    Sending = 4,
    Acknowledged = 5,
    Indeterminate = 6,
    RetryEligible = 7,
    FailedTerminal = 8
}

public enum InvoiceDispatchEvent
{
    Sign = 1,
    Reserve = 2,
    BeginSend = 3,
    ReceiptReceived = 4,
    OutcomeUnknown = 5,
    FailBeforeSend = 6,
    DefinitiveFailure = 7,
    AuthorizeSafeRetry = 8,
    ReconcileReceipt = 9,
    ReconcileNotReceived = 10,
    RetryFromEvidence = 11
}

/// <summary>
/// A receipt proves a submission acknowledgement only; never fiscal acceptance.
/// An unknown outcome cannot go back to Sending without authoritative evidence.
/// </summary>
public static class InvoiceDispatchPolicy
{
    public static bool TryTransition(
        InvoiceDispatchState current, InvoiceDispatchEvent action,
        out InvoiceDispatchState next)
    {
        next = current;
        if (!Enum.IsDefined(current) || !Enum.IsDefined(action)) return false;

        var target = (current, action) switch
        {
            (InvoiceDispatchState.Prepared, InvoiceDispatchEvent.Sign) => InvoiceDispatchState.Signed,
            (InvoiceDispatchState.Signed, InvoiceDispatchEvent.Reserve) => InvoiceDispatchState.Reserved,
            (InvoiceDispatchState.Reserved, InvoiceDispatchEvent.BeginSend) => InvoiceDispatchState.Sending,
            (InvoiceDispatchState.Reserved, InvoiceDispatchEvent.FailBeforeSend) => InvoiceDispatchState.RetryEligible,
            (InvoiceDispatchState.Sending, InvoiceDispatchEvent.ReceiptReceived) => InvoiceDispatchState.Acknowledged,
            (InvoiceDispatchState.Sending, InvoiceDispatchEvent.OutcomeUnknown) => InvoiceDispatchState.Indeterminate,
            (InvoiceDispatchState.Sending, InvoiceDispatchEvent.DefinitiveFailure) => InvoiceDispatchState.FailedTerminal,
            (InvoiceDispatchState.Indeterminate, InvoiceDispatchEvent.ReconcileReceipt) => InvoiceDispatchState.Acknowledged,
            (InvoiceDispatchState.Indeterminate, InvoiceDispatchEvent.ReconcileNotReceived) => InvoiceDispatchState.RetryEligible,
            (InvoiceDispatchState.RetryEligible, InvoiceDispatchEvent.RetryFromEvidence) => InvoiceDispatchState.Reserved,
            _ => (InvoiceDispatchState)0
        };
        if (target == 0) return false;
        next = target;
        return true;
    }

    public static bool RequiresReconciliation(InvoiceDispatchState current) =>
        current == InvoiceDispatchState.Indeterminate;

    public static bool IsInFlight(InvoiceDispatchState current) =>
        current is InvoiceDispatchState.Reserved or InvoiceDispatchState.Sending;

    public static bool IsReceiptOnly(InvoiceDispatchState current) =>
        current == InvoiceDispatchState.Acknowledged;
}

/// <summary>
/// Sanitized, stable classifications; no raw provider exception, response, token or payload.
/// Retry eligibility is a separate evidence-based decision, never inferred from HTTP status alone.
/// </summary>
public enum InvoiceDispatchFailureCode
{
    None = 0,
    Unauthorized = 1,
    ScopeMismatch = 2,
    UnsupportedPattern = 3,
    UnsupportedSignatureProfile = 4,
    UnverifiedRules = 5,
    InvalidInvoice = 6,
    StaleRevision = 7,
    ConflictingDispatch = 8,
    SigningUnavailable = 9,
    AuthorityAuthenticationRejected = 10,
    AuthorityValidationRejected = 11,
    AuthorityUnavailable = 12,
    RateLimited = 13,
    Timeout = 14,
    MalformedResponse = 15,
    ResultUnknown = 16,
    PersistenceUnavailable = 17
}

public enum InvoiceDispatchErrorCategory
{
    Authorization = 1,
    BusinessValidation = 2,
    Conflict = 3,
    Connectivity = 4,
    Uncertain = 5
}

public sealed record InvoiceDispatchError(
    InvoiceDispatchFailureCode Code, InvoiceDispatchErrorCategory Category,
    string SafeMessageKey, bool RequiresOperatorReview)
{
    public static InvoiceDispatchError FromCode(InvoiceDispatchFailureCode code) => code switch
    {
        InvoiceDispatchFailureCode.Unauthorized or InvoiceDispatchFailureCode.ScopeMismatch =>
            new(code, InvoiceDispatchErrorCategory.Authorization, "invoice.dispatch.access_denied", false),
        InvoiceDispatchFailureCode.UnsupportedPattern =>
            new(code, InvoiceDispatchErrorCategory.BusinessValidation, "invoice.dispatch.unsupported_pattern", false),
        InvoiceDispatchFailureCode.UnsupportedSignatureProfile =>
            new(code, InvoiceDispatchErrorCategory.BusinessValidation, "invoice.dispatch.unsupported_profile", true),
        InvoiceDispatchFailureCode.UnverifiedRules =>
            new(code, InvoiceDispatchErrorCategory.BusinessValidation, "invoice.dispatch.unverified_rules", true),
        InvoiceDispatchFailureCode.InvalidInvoice =>
            new(code, InvoiceDispatchErrorCategory.BusinessValidation, "invoice.dispatch.invalid_invoice", false),
        InvoiceDispatchFailureCode.StaleRevision or InvoiceDispatchFailureCode.ConflictingDispatch =>
            new(code, InvoiceDispatchErrorCategory.Conflict, "invoice.dispatch.conflict", false),
        InvoiceDispatchFailureCode.SigningUnavailable or InvoiceDispatchFailureCode.PersistenceUnavailable =>
            new(code, InvoiceDispatchErrorCategory.Connectivity, "invoice.dispatch.internal_unavailable", true),
        InvoiceDispatchFailureCode.AuthorityAuthenticationRejected =>
            new(code, InvoiceDispatchErrorCategory.Authorization, "invoice.dispatch.authority_auth_rejected", true),
        InvoiceDispatchFailureCode.AuthorityValidationRejected =>
            new(code, InvoiceDispatchErrorCategory.BusinessValidation, "invoice.dispatch.authority_rejected", false),
        InvoiceDispatchFailureCode.AuthorityUnavailable or InvoiceDispatchFailureCode.RateLimited =>
            new(code, InvoiceDispatchErrorCategory.Connectivity, "invoice.dispatch.authority_unavailable", true),
        InvoiceDispatchFailureCode.Timeout or InvoiceDispatchFailureCode.MalformedResponse
            or InvoiceDispatchFailureCode.ResultUnknown =>
            new(code, InvoiceDispatchErrorCategory.Uncertain, "invoice.dispatch.outcome_unknown", true),
        _ => throw new ArgumentOutOfRangeException(nameof(code), "A real failure code is required.")
    };
}

/// <summary>
/// Pure immutable transition proposal, not database persistence or authorization.
/// Revision is separate from the invoice's own immutable revision.
/// </summary>
public sealed record InvoiceDispatchSnapshot
{
    public Guid OperationId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public Guid FiscalMemoryId { get; }
    public Guid InvoiceId { get; }
    public long InvoiceRevision { get; }
    public string PayloadSha256 { get; }
    public InvoiceDispatchState State { get; }
    public long Revision { get; }

    private InvoiceDispatchSnapshot(Guid operationId, Guid customerUserId,
        Guid taxpayerId, Guid fiscalMemoryId, Guid invoiceId, long invoiceRevision,
        string payloadSha256, InvoiceDispatchState state, long revision)
    {
        OperationId = operationId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        FiscalMemoryId = fiscalMemoryId;
        InvoiceId = invoiceId;
        InvoiceRevision = invoiceRevision;
        PayloadSha256 = payloadSha256;
        State = state;
        Revision = revision;
    }

    public static InvoiceDispatchSnapshot Prepare(Guid operationId, Guid customerUserId,
        Guid taxpayerId, Guid fiscalMemoryId, Guid invoiceId, long invoiceRevision,
        string payloadSha256)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || fiscalMemoryId == Guid.Empty ||
            invoiceId == Guid.Empty || invoiceRevision < 1 ||
            !InvoiceValidationReceipt.IsSha256(payloadSha256))
            throw new ArgumentException("Invalid immutable dispatch identity.");

        return new(operationId, customerUserId, taxpayerId, fiscalMemoryId, invoiceId,
            invoiceRevision, payloadSha256.ToLowerInvariant(), InvoiceDispatchState.Prepared, 1);
    }

    public InvoiceDispatchSnapshot Propose(InvoiceDispatchEvent action, long expectedRevision)
    {
        if (expectedRevision != Revision)
            throw new InvalidOperationException("Stale dispatch revision.");
        if (!InvoiceDispatchPolicy.TryTransition(State, action, out var target))
            throw new InvalidOperationException("Dispatch transition is not allowed.");
        // This proposal does not prove that a receipt exists or that reconciliation was
        // successful. The later application service must validate event evidence.
        return new(OperationId, CustomerUserId, TaxpayerId, FiscalMemoryId,
            InvoiceId, InvoiceRevision, PayloadSha256, target, checked(Revision + 1));
    }
}
