using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

/// <summary>
/// Development DB custody KEK stays OUTSIDE PostgreSQL. Never put this key in source control,
/// the DB connection string or DB backups. Losing it makes encrypted taxpayer keys unreadable.
/// </summary>
internal sealed class DatabaseKeyProtectionProvider : IKeyProtectionProvider, IDisposable
{
    internal const string KeyVersion = "db:v1";
    private readonly byte[] masterKey;

    public DatabaseKeyProtectionProvider(IConfiguration configuration)
    {
        var encoded = configuration["SigningKeyProtection:DatabaseMasterKey"];
        if (string.IsNullOrWhiteSpace(encoded))
            throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
        try { masterKey = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new KeyProtectionException(KeyProtectionFailure.Unavailable); }
        if (masterKey.Length == 32) return;
        CryptographicOperations.ZeroMemory(masterKey);
        throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
    }

    public Task<WrappedDataKey> WrapAsync(KeyProtectionIdentity id, ReadOnlyMemory<byte> dek, CancellationToken ct)
    {
        id.Validate();
        ct.ThrowIfCancellationRequested();
        if (dek.Length != 32) throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[32];
        var tag = new byte[16];
        var joined = new byte[nonce.Length + ciphertext.Length + tag.Length];
        try
        {
            using (var aes = new AesGcm(masterKey, 16))
                aes.Encrypt(nonce, dek.Span, ciphertext, tag, id.TransitBinding());
            nonce.CopyTo(joined, 0);
            ciphertext.CopyTo(joined, nonce.Length);
            tag.CopyTo(joined, nonce.Length + ciphertext.Length);
            return Task.FromResult(new WrappedDataKey(KeyVersion + ":" + Convert.ToBase64String(joined), KeyVersion));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(joined);
        }
    }

    public Task<SensitiveBytes> UnwrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct)
    {
        id.Validate();
        ct.ThrowIfCancellationRequested();
        if (wrapped.ProtectionKeyVersion != KeyVersion || wrapped.Ciphertext is null ||
            !wrapped.Ciphertext.StartsWith(KeyVersion + ":", StringComparison.Ordinal))
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        byte[] joined;
        try { joined = Convert.FromBase64String(wrapped.Ciphertext[(KeyVersion.Length + 1)..]); }
        catch (FormatException) { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
        var plaintext = new byte[32];
        try
        {
            if (joined.Length != 60) throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            using (var aes = new AesGcm(masterKey, 16))
                aes.Decrypt(joined.AsSpan(0, 12), joined.AsSpan(12, 32), joined.AsSpan(44, 16),
                    plaintext, id.TransitBinding());
            var result = SensitiveBytes.TakeOwnership(plaintext);
            plaintext = Array.Empty<byte>();
            return Task.FromResult(result);
        }
        catch (CryptographicException) { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
        finally
        {
            CryptographicOperations.ZeroMemory(joined);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task<WrappedDataKey> RewrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct)
    {
        using var plaintext = await UnwrapAsync(id, wrapped, ct);
        return await WrapAsync(id, plaintext.Bytes, ct);
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(masterKey);
}
