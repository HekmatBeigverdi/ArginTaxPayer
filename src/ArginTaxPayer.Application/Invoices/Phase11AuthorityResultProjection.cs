namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Read-only display projection of an already correlated authority inquiry.
/// No fiscal lifecycle transition, dispatch retry, or accounting side effect.
/// </summary>
public static class Phase11AuthorityResultProjection
{
    public static Phase11AuthorityResultDecision Evaluate(
        InvoiceReconciliationDecision correlation,
        LegacyAuthorityInquiryReport? report,
        string? expectedReference)
    {
        ArgumentNullException.ThrowIfNull(correlation);

        if (correlation.Outcome == InvoiceReconciliationOutcome.ConflictingEvidence)
            return new("Conflict", "inquiry_identity_conflict");
        if (correlation.Outcome != InvoiceReconciliationOutcome.CorrelatedPendingReview ||
            report is null || expectedReference is null ||
            !string.Equals(correlation.MatchedReference, expectedReference, StringComparison.Ordinal))
            return new("Unconfirmed", "inquiry_no_verified_report");

        // SUCCESS is a report label, not proof of final fiscal acceptance.
        // FAILED may be displayed but does not authorize an invoice resend.
        return report.Status switch
        {
            "SUCCESS" => new("ReportedSuccess", "inquiry_reported_success"),
            "FAILED" => new("ReportedFailure", "inquiry_reported_failure"),
            "PENDING" => new("Pending", "inquiry_reported_pending"),
            _ => new("Unconfirmed", "inquiry_report_unrecognized")
        }
    }
}

public sealed record Phase11AuthorityResultDecision(string DisplayState, string SafeCode)
{
    public bool IsFiscalAccepted => false;
    public bool MayResendInvoice => false;
    public bool MayChangeLegalInvoice => false;
};
