using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Server-side read-only operation-to-endpoint routing. The caller must
/// independently verify authentication, submission consent, lifecycle and
/// durable send-attempt state before invoking any transport. No HTTP here.
/// </summary>
public sealed class Phase18AuthorityEndpointRoutingService(
    Phase18PersistedAuthorityOperationGate gate, IConfiguration configuration,
    IAuthorityInvoiceProvider protocol)
{
    public async Task<Uri?> ResolveAsync(Guid operationId, Guid customerUserId,
        Guid taxpayerId, Guid invoiceId, TaxAuthorityEnvironment registeredProvider,
        string protocolOperation, CancellationToken ct = default)
    {
        if (registeredProvider is TaxAuthorityEnvironment.Unknown ||
            !Enum.IsDefined(registeredProvider))
            return null;
        if (!AuthorityProtocolProfile.CanUseLegacyRoute(protocol, registeredProvider))
            return null;

        // Read BOTH database-stored classifications: immutable operation and
        // fiscal memory; never accept the environment from the HTTP request.
        var binding = await gate.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, ct);
        if (binding is not { } persisted ||
            persisted.Environment != registeredProvider)
            return null;
        return Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            persisted, persisted, registeredProvider, configuration,
            protocolOperation, out var endpoint) ? endpoint : null;
    }
}
