using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Phase10 Step12: deterministic, fail-closed handling of transport faults.
/// A local timeout, HTTP status, socket reset or process crash cannot prove
/// that the tax authority did not receive an invoice.
/// </summary>
public enum InvoiceTransportFailureKind
{
    BeforeNetworkAttempt = 1,
    LocalPreparationFailed = 2,
    TimeoutAfterStart = 3,
    ConnectionLostAfterStart = 4,
    ProviderUnavailableAfterStart = 5,
    RateLimitedAfterStart = 6,
    MalformedProviderResponse = 7,
    ProcessInterrupted = 8,
    UnknownAfterStart = 9
}

public enum InvoiceRetryDisposition
{
    SafeToQueueWithoutNetworkAttempt = 1,
    ReconciliationRequired = 2,
    NoRetryTerminal = 3
}

public sealed record InvoiceRetryDecision(
    InvoiceRetryDisposition Disposition,
    InvoiceDispatchEvent? ProposedEvent,
    InvoiceDispatchState? ProposedState,
    string SafeCode)
{
    public bool MayAutomaticallyResend => false;
    public bool IsFiscalSubmissionAuthorized => false;
}

/// <summary>
/// Fail-closed policy, not an I/O scheduler. State transitions must be verified
/// against current persisted state and revision in one atomic DB update.
/// </summary>
public static class Phase10RetrySafetyPolicy
{
    public static InvoiceRetryDecision Classify(
        InvoiceDispatchState persistedState, InvoiceTransportFailureKind failure)
    {
        if (!Enum.IsDefined(persistedState) || !Enum.IsDefined(failure))
            throw new ArgumentOutOfRangeException(nameof(failure), "Unknown dispatch state/failure.");

        if (persistedState is InvoiceDispatchState.Acknowledged or
            InvoiceDispatchState.FailedTerminal)
            return new(InvoiceRetryDisposition.NoRetryTerminal, null, null,
                "invoice.retry.terminal_no_retry");

        if (persistedState == InvoiceDispatchState.Reserved &&
            failure is InvoiceTransportFailureKind.BeforeNetworkAttempt or
                InvoiceTransportFailureKind.LocalPreparationFailed)
            return new(InvoiceRetryDisposition.SafeToQueueWithoutNetworkAttempt,
                InvoiceDispatchEvent.FailBeforeSend, InvoiceDispatchState.RetryEligible,
                "invoice.retry.failed_before_send");

        if (persistedState == InvoiceDispatchState.Sending ||
            persistedState == InvoiceDispatchState.Indeterminate ||
            (persistedState == InvoiceDispatchState.Reserved &&
             failure is not (InvoiceTransportFailureKind.BeforeNetworkAttempt or
                 InvoiceTransportFailureKind.LocalPreparationFailed)))
        {
            // Explicitly block Reserved -> Indeterminate as it is not a supported
            // durable state transition; a worker must first prove a Sending
            // CAS and preserve attempt evidence. Otherwise stop and review.
            if (persistedState == InvoiceDispatchState.Reserved)
                return new(InvoiceRetryDisposition.ReconciliationRequired, null, null,
                    "invoice.retry.reserved_ambiguous_investigate");
            if (persistedState == InvoiceDispatchState.Indeterminate)
                return new(InvoiceRetryDisposition.ReconciliationRequired, null, null,
                    "invoice.retry.await_reconciliation");
            return new(InvoiceRetryDisposition.ReconciliationRequired,
                InvoiceDispatchEvent.OutcomeUnknown, InvoiceDispatchState.Indeterminate,
                "invoice.retry.outcome_unknown");
        }

        return new(InvoiceRetryDisposition.NoRetryTerminal, null, null,
            "invoice.retry.transition_not_allowed");
    }

    /// <summary>
    /// All transitions require durable evidence. Never infer a safe retry from
    /// HTTP 408/429/500, expired leases or a locally cancelled request.
    /// </summary>
    public static bool TryPlanPersistedTransition(
        InvoiceDispatchState current,
        InvoiceDispatchEvent requested,
        long expectedRevision,
        out InvoiceDispatchState next)
    {
        next = current;
        if (expectedRevision < 1 ||
            requested is not (InvoiceDispatchEvent.FailBeforeSend or
                InvoiceDispatchEvent.OutcomeUnknown))
            return false;
        return InvoiceDispatchPolicy.TryTransition(current, requested, out next);
    }
}
