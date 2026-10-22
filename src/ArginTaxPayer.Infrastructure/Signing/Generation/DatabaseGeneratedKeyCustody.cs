using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

/// <summary>Same operation journal and receipt contract as Vault; storage remains provider-neutral.</summary>
internal sealed class DatabaseGeneratedKeyCustody(ProtectedKeyEnvelopeStore store, KeyEnvelopeProtector protector,
    ArginTaxPayerDbContext db) : IGeneratedKeyCustody
{
    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        // Fail before reserving/claiming an operation if the optional DB migration is absent.
        try { await db.DbSigningKeyEnvelopes.AsNoTracking().Take(1).AnyAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new KeyProtectionException(KeyProtectionFailure.Unavailable); }
    }

    public async Task<SealedGeneratedKey> StageAsync(KeyProtectionIdentity id, GeneratedRsaKey key, CancellationToken ct)
    {
        var stored = await store.StageAsync(id, key.PrivateMaterial.Bytes, ct);
        var receipt = await ReceiptAsync(id, stored, ct);
        if (receipt.PublicFingerprintSha256 != key.PublicFingerprintSha256)
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        return receipt;
    }

    public async Task<SealedGeneratedKey?> RecoverAsync(KeyProtectionIdentity id, CancellationToken ct)
    {
        var stored = await store.RecoverAsync(id, ct);
        return stored is null ? null : await ReceiptAsync(id, stored, ct);
    }

    private async Task<SealedGeneratedKey> ReceiptAsync(KeyProtectionIdentity id, StoredKeyEnvelope stored,
        CancellationToken ct)
    {
        using var material = await protector.UnprotectAsync(id, stored.Envelope, ct);
        var publicDer = RsaSigningKeyMaterial.DerivePublic(material.Bytes);
        try { return new(id, publicDer, RsaSigningKeyMaterial.Fingerprint(publicDer), stored.Envelope.ProtectionKeyVersion); }
        catch { CryptographicOperations.ZeroMemory(publicDer); throw; }
    }
}
