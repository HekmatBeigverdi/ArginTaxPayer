namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Step13 safe interpretation handoff to the Step10 receipt policy.
/// This does not perform network calls or authorize dispatch.
/// </summary>
public static class Phase10AuthorityResponseHandoff
{
    public static InvoiceSubmissionTransportOutcome ToTransportOutcome(
        Guid operationId, InvoiceAuthorityResponseDecision decision, DateTime observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (operationId == Guid.Empty || observedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Trusted operation ID and UTC observation time required.");
        return decision.Classification switch
        {
            InvoiceAuthorityResponseClassification.InitialReceipt
                when decision.ReferenceNumber is { Length: > 0 } =>
                new(operationId, InvoiceSubmissionReceiptStatus.Acknowledged,
                    decision.ReferenceNumber, "invoice.dispatch.initial_receipt_recorded",
                    observedAtUtc),
            // Step13 has not yet verified an official definitive-rejection matrix.
            // Even a claimed rejection remains uncertain until source-specific evidence.
            _ => new(operationId, InvoiceSubmissionReceiptStatus.Indeterminate,
                    null, "invoice.dispatch.response_indeterminate", observedAtUtc)
        };
    }
}
