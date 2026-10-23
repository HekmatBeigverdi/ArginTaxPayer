using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

internal sealed class VaultGeneratedKeyCustody(ProtectedKeyEnvelopeStore store, KeyEnvelopeProtector protector,
    VaultHttpTransport transport, VaultProtectionOptions options) : IGeneratedKeyCustody
{
    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        using var result = await transport.SendAsync(HttpMethod.Get,
            $"v1/{options.TransitMount}/keys/{options.TransitKey}", null, false, false, ct);
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

    private async Task<SealedGeneratedKey> ReceiptAsync(KeyProtectionIdentity id, StoredKeyEnvelope stored, CancellationToken ct)
    {
        using var material = await protector.UnprotectAsync(id, stored.Envelope, ct);
        var spki = RsaSigningKeyMaterial.DerivePublic(material.Bytes);
        return new(id, spki, RsaSigningKeyMaterial.Fingerprint(spki), stored.Envelope.ProtectionKeyVersion);
    }
}
