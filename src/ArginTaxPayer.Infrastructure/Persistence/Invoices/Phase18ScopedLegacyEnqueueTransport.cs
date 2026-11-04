using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Signing.Connection;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Restricted legacy HTTP adapter for an already-admitted operation. Origin is
/// selected ONLY by a joined database read of the immutable dispatch operation
/// and its fiscal memory. This adapter does not create/send an outbox message,
/// retry an indeterminate send, or assert fiscal acceptance. Only the durable
/// attempt-owning worker may call it after acquiring the send attempt.
/// </summary>
internal sealed class Phase18ScopedLegacyEnqueueTransport(
    Phase18AuthorityEndpointRoutingService route,
    LegacyInvoiceSubmissionHttpTransport http)
{
    internal async Task<LegacyInvoiceSubmissionHttpTransport.Result> PostOnceAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid invoiceId,
        TaxAuthorityEnvironment registeredProvider, string fiscalId,
        string signedRequestJson, string transferSignature, string accessToken,
        string requestTraceId, string timestamp, CancellationToken ct)
    {
        var endpoint = await route.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, registeredProvider, "ENQUEUE", ct);
        if (endpoint is null)
            return new(false, "authority_operation_environment_not_authorized");

        // The existing validated adapter handles packet identity, response UID,
        // bounded response, timeout, and indeterminate results identically for
        // both origins. No retry is performed here.
        return await http.PostVerifiedEndpointAsync(endpoint, fiscalId,
            signedRequestJson, transferSignature, accessToken,
            requestTraceId, timestamp, ct);
    }
}
