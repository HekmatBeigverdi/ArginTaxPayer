using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

internal sealed record DatabaseLegacySigningKey(
    SensitiveBytes PrivateKey, byte[] PublicSpkiDer) : IDisposable
{
    public void Dispose()
    {
        PrivateKey.Dispose();
        CryptographicOperations.ZeroMemory(PublicSpkiDer);
    }
}

/// <summary>
/// Loads the selected tenant key from database custody. The database stores
/// only an encrypted PKCS#8 envelope; plaintext exists only in this scope.
/// </summary>
internal sealed class DatabaseLegacySigningKeyLoader(
    ArginTaxPayerDbContext db, ProtectedKeyEnvelopeStore custody,
    KeyEnvelopeProtector protector)
{
    internal async Task<DatabaseLegacySigningKey?> RecoverAsync(
        Guid customerUserId, Guid taxpayerId, Guid fiscalMemoryId,
        CancellationToken ct = default)
    {
        if (customerUserId == Guid.Empty || taxpayerId == Guid.Empty || fiscalMemoryId == Guid.Empty)
            return null;

        var selected = await db.SigningKeySelections.AsNoTracking()
            .Where(x => x.CustomerUserId == customerUserId && x.TaxpayerId == taxpayerId &&
                        x.FiscalMemoryId == fiscalMemoryId)
            .Select(x => (Guid?)x.KeyVersionId)
            .SingleOrDefaultAsync(ct);
        var query = db.SigningKeyVersions.AsNoTracking().Where(x =>
            x.CustomerUserId == customerUserId && x.TaxpayerId == taxpayerId &&
            x.FiscalMemoryId == fiscalMemoryId && x.LifecycleState == 2 &&
            x.PublicSpkiDer != null && x.CustodyReference != null);
        var key = selected is { } id
            ? await query.SingleOrDefaultAsync(x => x.Id == id, ct)
            : await query.OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        if (key is null || key.PublicSpkiDer is not { Length: > 0 and <= 4096 } publicSpki ||
            key.OriginOperationRecordId == Guid.Empty || key.CustodyReference is not { } custodyReference ||
            !custodyReference.StartsWith("db://", StringComparison.Ordinal))
            return null;

        var identity = new KeyProtectionIdentity(customerUserId, taxpayerId,
            fiscalMemoryId, key.Id, key.OriginOperationRecordId);
        var stored = await custody.RecoverAsync(identity, ct);
        if (stored is null) return null;
        var privateKey = await protector.UnprotectAsync(identity, stored.Envelope, ct);
        try
        {
            var copy = (byte[])publicSpki.Clone();
            return new(privateKey, copy);
        }
        catch
        {
            privateKey.Dispose();
            throw;
        }
    }
}
