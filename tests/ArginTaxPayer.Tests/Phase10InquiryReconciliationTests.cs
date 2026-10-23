using ArginTaxPayer.Application.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10InquiryReconciliationTests
{
    private const string Uid = "aabbccdd-1234-4567-8910-abcd12345678";
    private const string FiscalId = "ABCD123456";
    private const string Reference = "REF123456";
    private static InvoiceReconciliationIdentity Identity() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Uid, "INVOICE.V01", FiscalId, Reference);

    private static string Response(string status = "SUCCESS") => $$"""
        [{
          "uid":"{{Uid}}",
          "packetType":"INVOICE.V01",
          "fiscalId":"{{FiscalId}}",
          "referenceNumber":"{{Reference}}",
          "status":"{{status}}",
          "data":{"success":true},
          "sign":"UNVERIFIED_ARCHIVED_SDK_VALUE"
        }]
        """;

    [Theory]
    [InlineData("SUCCESS")]
    [InlineData("FAILED")]
    [InlineData("PENDING")]
    [InlineData("UNKNOWN")]
    public void Correlation_does_not_claim_final_fiscal_outcome(string status)
    {
        var result = Phase10InquiryReconciliation.Evaluate(Identity(), Response(status));
        Assert.Equal(InvoiceReconciliationOutcome.CorrelatedPendingReview, result.Outcome);
        Assert.Equal(Reference, result.MatchedReference);
        Assert.False(result.IsFiscalAccepted);
        Assert.False(result.ProvesNotDelivered);
        Assert.False(result.MayAutomaticallyResend);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("[{}]")]
    [InlineData(null)]
    public void Empty_or_malformed_inquiry_never_proves_non_delivery(string? raw)
    {
        var result = Phase10InquiryReconciliation.Evaluate(Identity(), raw);
        Assert.Equal(InvoiceReconciliationOutcome.NoConclusiveResult, result.Outcome);
        Assert.False(result.ProvesNotDelivered);
        Assert.False(result.MayAutomaticallyResend);
    }

    [Fact]
    public void Cross_fiscal_identity_and_reference_are_conflicts()
    {
        var expected = Identity();
        foreach (var json in new[] {
            Response().Replace(FiscalId, "OTHER123"),
            Response().Replace(Reference, "OTHERREF"),
            Response().Replace(Uid, "another-uid"),
            Response().Replace("INVOICE.V01", "OTHER")
        })
        {
            var decision = Phase10InquiryReconciliation.Evaluate(expected, json);
            Assert.Equal(InvoiceReconciliationOutcome.ConflictingEvidence, decision.Outcome);
            Assert.Null(decision.MatchedReference);
            Assert.False(decision.MayAutomaticallyResend);
        }
    }

    [Fact]
    public void Duplicated_response_or_duplicate_properties_cannot_be_chosen_arbitrarily()
    {
        var json = Response();
        var duplicate = json.Replace("\"status\":\"SUCCESS\"",
            "\"status\":\"SUCCESS\",\"STATUS\":\"FAILED\"");
        var result = Phase10InquiryReconciliation.Evaluate(Identity(), duplicate);
        Assert.Equal(InvoiceReconciliationOutcome.NoConclusiveResult, result.Outcome);
        var multiple = "[" + json.Trim()[1..^1] + "," + json.Trim()[1..^1] + "]";
        Assert.Equal(InvoiceReconciliationOutcome.ConflictingEvidence,
            Phase10InquiryReconciliation.Evaluate(Identity(), multiple).Outcome);
    }

    [Fact]
    public async Task Disabled_inquiry_cannot_contact_provider_or_grant_resend()
    {
        var result = await new DisabledInvoiceAuthorityInquiry().InquireAsync(Identity());
        Assert.Equal(InvoiceReconciliationOutcome.NoConclusiveResult, result.Outcome);
        Assert.False(result.IsFiscalSubmissionAuthorized);
        Assert.False(result.MayAutomaticallyResend);
    }

    [Fact]
    public async Task Canceled_inquiry_is_not_executed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DisabledInvoiceAuthorityInquiry().InquireAsync(Identity(), cts.Token));
    }
}
