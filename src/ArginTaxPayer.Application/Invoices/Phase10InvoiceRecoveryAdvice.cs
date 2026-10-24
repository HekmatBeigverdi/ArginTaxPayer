namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Step19 safe recovery UI intent. This is *advice*, never an authorization
/// for provider network calls, retries, fiscal acceptance or mutation.
/// </summary>
public enum InvoiceRecoveryNextAction
{
    CompleteDraft,
    AwaitPreparation,
    RefreshOnly,
    AwaitReconciliation,
    ManualReview,
    NoFurtherSend
}
public sealed record InvoiceRecoveryAdvice(
    InvoiceRecoveryNextAction NextAction, string SafeCode,
    bool MaySend, bool MayRetry, bool MayInquire, bool IsFiscalAccepted);

public static class Phase10InvoiceRecoveryAdvice
{
    public static InvoiceRecoveryAdvice ForState(string? state, bool isWorkspace)
    {
        if(isWorkspace)
            return new(InvoiceRecoveryNextAction.CompleteDraft,
                "invoice.recovery.complete_draft",false,false,false,false);
        return state switch {
            "NotPrepared" or "Prepared" or "Signed" =>
                new(InvoiceRecoveryNextAction.AwaitPreparation,
                    "invoice.recovery.await_preparation",false,false,false,false),
            "Reserved" or "Sending" or "Indeterminate" =>
                new(InvoiceRecoveryNextAction.AwaitReconciliation,
                    "invoice.recovery.reconcile_before_retry",false,false,false,false),
            "RetryEligible" =>
                new(InvoiceRecoveryNextAction.ManualReview,
                    "invoice.recovery.manual_review_only",false,false,false,false),
            "Acknowledged" =>
                new(InvoiceRecoveryNextAction.RefreshOnly,
                    "invoice.recovery.receipt_not_fiscal_acceptance",false,false,false,false),
            "FailedTerminal" =>
                new(InvoiceRecoveryNextAction.NoFurtherSend,
                    "invoice.recovery.terminal_no_resend",false,false,false,false),
            _ => new(InvoiceRecoveryNextAction.ManualReview,
                "invoice.recovery.unrecognized_stop",false,false,false,false)
        };
    }
}
