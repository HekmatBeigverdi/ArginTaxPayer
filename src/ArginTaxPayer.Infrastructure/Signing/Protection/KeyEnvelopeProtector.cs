using System.Security.Cryptography;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

internal sealed class KeyEnvelopeProtector(IKeyProtectionProvider provider)
{
    public const int MaximumKeyBytes = 16 * 1024;
    public const string Algorithm = "AES-256-GCM";

    // The caller owns input and must dispose/zero it. PKCS#8 parsing is the generation/import boundary's job.
    public async Task<ProtectedKeyEnvelope> ProtectAsync(KeyProtectionIdentity id,
        ReadOnlyMemory<byte> pkcs8, CancellationToken ct = default)
    {
        id.Validate();
        if (pkcs8.Length is < 1 or > MaximumKeyBytes)
            throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);
        ct.ThrowIfCancellationRequested();
        var dek = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[pkcs8.Length];
        var tag = new byte[16];
        try
        {
            using (var aes = new AesGcm(dek, 16))
                aes.Encrypt(nonce, pkcs8.Span, ciphertext, tag, id.Aad());
            var wrapped = await provider.WrapAsync(id, dek, ct);
            if (string.IsNullOrWhiteSpace(wrapped.Ciphertext) ||
                string.IsNullOrWhiteSpace(wrapped.ProtectionKeyVersion))
                throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            var result = new ProtectedKeyEnvelope(1, Algorithm, id, nonce, ciphertext, tag,
                wrapped.Ciphertext, wrapped.ProtectionKeyVersion);
            Validate(id, result);
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(dek); }
    }

    public static void Validate(KeyProtectionIdentity id, ProtectedKeyEnvelope e)
    {
        id.Validate();
        if (e is null || e.Identity != id || e.FormatVersion != 1 || e.AlgorithmId != Algorithm ||
            e.Nonce is not { Length: 12 } || e.Tag is not { Length: 16 } ||
            e.Ciphertext is not { Length: > 0 and <= MaximumKeyBytes } ||
            string.IsNullOrWhiteSpace(e.WrappedDek) || e.WrappedDek.Length > 4096 ||
            string.IsNullOrWhiteSpace(e.ProtectionKeyVersion) || e.ProtectionKeyVersion.Length > 64)
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
    }

    public async Task<SensitiveBytes> UnprotectAsync(KeyProtectionIdentity id,
        ProtectedKeyEnvelope envelope, CancellationToken ct = default)
    {
        Validate(id, envelope);
        ct.ThrowIfCancellationRequested();
        using var dek = await provider.UnwrapAsync(id,
            new(envelope.WrappedDek, envelope.ProtectionKeyVersion), ct);
        if (dek.Bytes.Length != 32) throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        var plaintext = new byte[envelope.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(dek.Bytes.Span, 16);
            aes.Decrypt(envelope.Nonce, envelope.Ciphertext, envelope.Tag, plaintext, id.Aad());
            return SensitiveBytes.TakeOwnership(plaintext);
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        }
    }

    public async Task<ProtectedKeyEnvelope> RewrapAsync(KeyProtectionIdentity id,
        ProtectedKeyEnvelope envelope, CancellationToken ct = default)
    {
        Validate(id, envelope);
        // Authenticate the existing complete envelope before accepting a new wrapping version.
        using var clear = await UnprotectAsync(id, envelope, ct);
        var wrapped = await provider.RewrapAsync(id,
            new(envelope.WrappedDek, envelope.ProtectionKeyVersion), ct);
        var result = envelope with
        {
            WrappedDek = wrapped.Ciphertext, ProtectionKeyVersion = wrapped.ProtectionKeyVersion
        };
        Validate(id, result);
        using var verified = await UnprotectAsync(id, result, ct);
        if (!CryptographicOperations.FixedTimeEquals(clear.Bytes.Span, verified.Bytes.Span))
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        return result;
    }
}
