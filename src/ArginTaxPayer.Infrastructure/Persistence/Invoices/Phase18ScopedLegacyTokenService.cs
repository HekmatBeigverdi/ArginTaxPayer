using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Scoped legacy GET_TOKEN for a persisted and classified operation. Unlike
/// Phase10's historic Sandbox custody, this path only uses the chosen
/// fiscal_memories row and its database-protected selected signing key.
/// This is not a dispatch service or a go-live authorization.
/// </summary>
internal sealed class Phase18ScopedLegacyTokenService(
    ArginTaxPayerDbContext db,
    Phase18PersistedAuthorityOperationGate gate,
    Phase18AuthorityEndpointRoutingService routes,
    DatabaseLegacySigningKeyLoader keys,
    LegacyInvoiceAccessTokenClient client)
{
    internal async Task<LegacyInvoiceAccessTokenResult> RequestAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid invoiceId,
        TaxAuthorityEnvironment registeredProvider, CancellationToken ct)
    {
        var binding = await gate.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, ct);
        if (binding is not { } persisted || persisted.Environment != registeredProvider)
            return new(false, "authority_operation_environment_not_authorized");

        var tokenEndpoint = await routes.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, registeredProvider, "GET_TOKEN", ct);
        if (tokenEndpoint is null)
            return new(false, "authority_token_route_not_authorized");

        var memoryCode = await db.FiscalMemories.AsNoTracking()
            .Where(m => m.Id == persisted.FiscalMemoryId &&
                        m.TaxpayerId == taxpayerId &&
                        m.Status == FiscalMemoryStatus.Active &&
                        m.AuthorityEnvironment != null)
            .Select(m => m.MemoryIdentifier)
            .SingleOrDefaultAsync(ct);
        if (memoryCode is not { Length: 6 } ||
            memoryCode.Any(c => !char.IsAsciiLetterOrDigit(c)))
            return new(false, "authority_memory_client_id_unavailable");

        // Key custody is tenant/memory-scoped and zeroized at end of request.
        using var key = await keys.RecoverAsync(customerUserId, taxpayerId,
            persisted.FiscalMemoryId, ct);
        if (key is null)
            return new(false, "authority_signing_key_unavailable");

        return await client.RequestVerifiedEndpointAsync(tokenEndpoint,
            memoryCode, key.PrivateKey, key.PublicSpkiDer, ct);
    }
}
