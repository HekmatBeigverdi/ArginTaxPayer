using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10SubmissionTransportTests
{
    private static readonly DateTime At = new(2026, 10, 23, 14, 0, 0, DateTimeKind.Utc);

    private static InvoiceSubmissionDispatchDescriptor Descriptor() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        3, new string('a', 64), "synthetic-idempotency", "tax-authority-submit");

    [Fact]
    public async Task Disabled_production_transport_makes_no_submission()
    {
        var d = Descriptor();
        var transport = new DisabledInvoiceSubmissionTransport();
        var result = await transport.SendAsync(d);
        Assert.Equal(d.OperationId, result.OperationId);
        Assert.Equal(InvoiceSubmissionReceiptStatus.Indeterminate, result.Status);
        Assert.Equal("invoice.dispatch.transport_disabled", result.SafeCode);
        Assert.Null(result.ProviderReference);
    }

    [Fact]
    public async Task Simulated_acknowledgement_is_not_fiscal_acceptance()
    {
        var d = Descriptor();
        var fake = new SimulatedInvoiceSubmissionTransport(x =>
            new(x.OperationId, InvoiceSubmissionReceiptStatus.Acknowledged,
                "synthetic-ref-001", "invoice.dispatch.receipt_recorded", At));
        var outcome = await fake.SendAsync(d);
        var receipt = InvoiceSubmissionReceiptPolicy.Parse(d, outcome);
        Assert.Equal("synthetic-ref-001", receipt.ProviderReference);
        Assert.False(receipt.IsFiscalAccepted);
        Assert.Equal(InvoiceDispatchEvent.ReceiptReceived,
            InvoiceSubmissionReceiptPolicy.SuggestedEvent(receipt.Status));
    }

    [Fact]
    public void Ambiguous_result_never_grants_retry_or_acknowledgement()
    {
        var d = Descriptor();
        var r = InvoiceSubmissionReceiptPolicy.Parse(d,
            new(d.OperationId, InvoiceSubmissionReceiptStatus.Indeterminate,
                null, "invoice.dispatch.outcome_unknown", At));
        Assert.False(r.IsFiscalAccepted);
        Assert.Null(r.ProviderReference);
        Assert.Equal(InvoiceDispatchEvent.OutcomeUnknown,
            InvoiceSubmissionReceiptPolicy.SuggestedEvent(r.Status));
        Assert.False(InvoiceDispatchPolicy.TryTransition(
            InvoiceDispatchState.Indeterminate, InvoiceDispatchEvent.BeginSend, out _));
    }

    [Fact]
    public void Explicit_rejection_requires_no_receipt_reference()
    {
        var d = Descriptor();
        var r = InvoiceSubmissionReceiptPolicy.Parse(d,
            new(d.OperationId, InvoiceSubmissionReceiptStatus.ExplicitlyRejected,
                null, "invoice.dispatch.authority_rejected", At));
        Assert.Equal(InvoiceDispatchEvent.DefinitiveFailure,
            InvoiceSubmissionReceiptPolicy.SuggestedEvent(r.Status));
        Assert.False(r.IsFiscalAccepted);
    }

    [Fact]
    public void Wrong_operation_or_incomplete_acknowledgement_is_denied()
    {
        var d = Descriptor();
        Assert.Throws<ArgumentException>(() => InvoiceSubmissionReceiptPolicy.Parse(d,
            new(Guid.NewGuid(), InvoiceSubmissionReceiptStatus.Acknowledged,
                "ref1", "invoice.dispatch.receipt_recorded", At)));
        Assert.Throws<ArgumentException>(() => InvoiceSubmissionReceiptPolicy.Parse(d,
            new(d.OperationId, InvoiceSubmissionReceiptStatus.Acknowledged,
                null, "invoice.dispatch.receipt_recorded", At)));
        Assert.Throws<ArgumentException>(() => InvoiceSubmissionReceiptPolicy.Parse(d,
            new(d.OperationId, InvoiceSubmissionReceiptStatus.Indeterminate,
                "ref1", "invoice.dispatch.outcome_unknown", At)));
    }

    [Fact]
    public void Malformed_wire_hash_is_denied()
    {
        var d = Descriptor() with { WireSha256 = "not-a-sha256" };

        Assert.Throws<ArgumentException>(() => InvoiceSubmissionReceiptPolicy.Parse(d,
            new(d.OperationId, InvoiceSubmissionReceiptStatus.Indeterminate,
                null, "invoice.dispatch.outcome_unknown", At)));
    }

    [Fact]
    public void Raw_response_and_untrusted_safe_code_cannot_cross_boundary()
    {
        var d = Descriptor();
        Assert.Throws<ArgumentException>(() => InvoiceSubmissionReceiptPolicy.Parse(d,
            new(d.OperationId, InvoiceSubmissionReceiptStatus.Acknowledged,
                "secret\nprovider-body", "invoice.dispatch.receipt_recorded", At)));
        Assert.Throws<ArgumentException>(() => InvoiceSubmissionReceiptPolicy.Parse(d,
            new(d.OperationId, InvoiceSubmissionReceiptStatus.Indeterminate,
                null, "error: raw tax authority response", At)));
        Assert.Throws<ArgumentException>(() => InvoiceSubmissionReceiptPolicy.Parse(
            d with { EndpointCode = "https://malicious.example" },
            new(d.OperationId, InvoiceSubmissionReceiptStatus.Indeterminate,
                null, "invoice.dispatch.outcome_unknown", At)));
    }

    [Fact]
    public async Task Test_transport_respects_cancellation()
    {
        var fake = new SimulatedInvoiceSubmissionTransport(_ =>
            throw new InvalidOperationException("Must not be invoked"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fake.SendAsync(Descriptor(), cts.Token));
    }
}
