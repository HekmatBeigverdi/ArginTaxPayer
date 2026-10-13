using System.Security.Cryptography;
using System.Text;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>Encryption key must be injected via secret configuration, not source control.</summary>
public sealed class IdentityMfaCrypto
{
    private readonly byte[] _key;

    public IdentityMfaCrypto(string base64Key)
    {
        try { _key = Convert.FromBase64String(base64Key); }
        catch (FormatException) { throw new InvalidOperationException("Invalid Identity MFA key."); }
        if (_key.Length != 32)
            throw new InvalidOperationException("Identity MFA encryption key must be 32 bytes.");
    }

    public string Encrypt(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(_key, 16))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var payload = new byte[12 + 16 + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, 12);
        ciphertext.CopyTo(payload, 28);
        CryptographicOperations.ZeroMemory(plaintext);
        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string encoded)
    {
        var payload = Convert.FromBase64String(encoded);
        if (payload.Length < 29) throw new CryptographicException("Invalid ciphertext.");
        var result = new byte[payload.Length - 28];
        try
        {
            using (var aes = new AesGcm(_key, 16))
                aes.Decrypt(payload.AsSpan(0, 12), payload.AsSpan(28), payload.AsSpan(12, 16), result);
            return Encoding.UTF8.GetString(result);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new CryptographicException("Invalid ciphertext.", ex);
        }
        finally { CryptographicOperations.ZeroMemory(result); }
    }
}
