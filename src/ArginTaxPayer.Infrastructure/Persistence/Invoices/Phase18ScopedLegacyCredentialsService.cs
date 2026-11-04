using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Loads legacy credentials for an existing classified, owner-scoped operation
/// through endpoints resolved from PostgreSQL. Each signing-key scope is
/// zeroized before returning token and public encryption metadata. It performs
/// no invoice enqueue and never persists a bearer token.
/// </summary>
internal sealed class Phase18ScopedLegacyCredentialsService(
    ArginTaxPayerDbContext db,
    Phase18PersistedAuthorityOperationGate gate,
    Phase18AuthorityEndpointRoutingService routes,
    DatabaseLegacySigningKeyLoader keys,
    LegacyInvoiceAccessTokenClient tokens,
    LegacySandboxServerEncryptionKeyClient encryption)
{
    internal async Task<LegacyInvoicePreparationCredentials?> RequestAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid invoiceId,
        TaxAuthorityEnvironment registeredProvider, CancellationToken ct)
    {
        var binding = await gate.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, ct);
        if (binding is not { } persisted || persisted.Environment != registeredProvider)
            return null;

        var tokenEndpoint = await routes.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, registeredProvider, "GET_TOKEN", ct);
        var serverInfoEndpoint = await routes.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, registeredProvider, "GET_SERVER_INFORMATION", ct);
        if (tokenEndpoint is null || serverInfoEndpoint is null ||
            tokenEndpoint.Host != serverInfoEndpoint.Host ||
            tokenEndpoint.Scheme != serverInfoEndpoint.Scheme)
            return null;

        var memoryCode = await db.FiscalMemories.AsNoTracking()
            .Where(m => m.Id == persisted.FiscalMemoryId &&
                m.TaxpayerId == taxpayerId &&
                m.Status == FiscalMemoryStatus.Active)
            .Select(m => m.MemoryIdentifier).SingleOrDefaultAsync(ct);
        if (memoryCode is not { Length: 6 } ||
            memoryCode.Any(c => !char.IsAsciiLetterOrDigit(c)))
            return null;

        using var key = await keys.RecoverAsync(customerUserId, taxpayerId,
            persisted.FiscalMemoryId, ct);
        if (key is null)
            return null;

        var credentials = await LegacyInvoicePreparationCredentials.RequestVerifiedAsync(
            tokens, encryption, tokenEndpoint, serverInfoEndpoint,
            memoryCode, key.PrivateKey, key.PublicSpkiDer, ct);
        if (credentials.Token.Authenticated && credentials.Token.AccessToken is not null &&
            credentials.EncryptionKey is not null)
            return credentials;

        credentials.Dispose();
        return null;
    }
}
