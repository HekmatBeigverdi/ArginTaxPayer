using ArginTaxPayer.Application.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase11AuthorityResultProjectionTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("SUCCESS", "ReportedSuccess")]
    [InlineData("FAILED", "ReportedFailure")]
    [InlineData("PENDING", "Pending")]
    [InlineData("UNKNOWN", "Unconfirmed")]
    public void Correlated_reports_are_display_only(string status, string expected)
    {
        var correlation = new InvoiceReconciliationDecision(
            InvoiceReconciliationOutcome.CorrelatedPendingReview,
            "invoice.reconciliation.correlated_pending_review", "ref123");
        var report = new LegacyAuthorityInquiryReport(status,
            Array.Empty<LegacyAuthorityIssue>(), Array.Empty<LegacyAuthorityIssue>(), false, Now);
        var result = Phase11AuthorityResultProjection.Evaluate(correlation, report, "ref123");
        Assert.Equal(expected, result.DisplayState);
        Assert.False(result.IsFiscalAccepted);
        Assert.False(result.MayResendInvoice);
        Assert.False(result.MayChangeLegalInvoice);
    }

    [Fact]
    public void Wrong_reference_never_projects_provider_status()
    {
        var correlation = new InvoiceReconciliationDecision(
            InvoiceReconciliationOutcome.CorrelatedPendingReview, "test", "ref123");
        var report = new LegacyAuthorityInquiryReport("SUCCESS",
            Array.Empty<LegacyAuthorityIssue>(), Array.Empty<LegacyAuthorityIssue>(), false, Now);
        Assert.Equal("Unconfirmed",
            Phase11AuthorityResultProjection.Evaluate(correlation, report, "other").DisplayState);
    }

    [Fact]
    public void Conflicting_evidence_is_never_a_final_success()
    {
        var correlation = new InvoiceReconciliationDecision(
            InvoiceReconciliationOutcome.ConflictingEvidence, "test");
        var result = Phase11AuthorityResultProjection.Evaluate(correlation, null, null);
        Assert.Equal("Conflict", result.DisplayState);
        Assert.False(result.IsFiscalAccepted);
    }

    [Fact]
    public void Missing_report_fails_closed()
    {
        var correlation = new InvoiceReconciliationDecision(
            InvoiceReconciliationOutcome.CorrelatedPendingReview, "test", "ref123");
        Assert.Equal("Unconfirmed",
            Phase11AuthorityResultProjection.Evaluate(correlation, null, "ref123").DisplayState);
    }
}
