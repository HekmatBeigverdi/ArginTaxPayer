using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Matching;

internal static class RsaKeyMatchProof
{
    public static bool Match(ReadOnlyMemory<byte> pkcs8, byte[] storedSpki, byte[] providedSpki)
    {
        var derived = RsaSigningKeyMaterial.DerivePublic(pkcs8);
        if (!derived.AsSpan().SequenceEqual(storedSpki))
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        try
        {
            using var privateKey = RSA.Create();
            privateKey.ImportPkcs8PrivateKey(pkcs8.Span, out _);
            using var providedKey = RSA.Create();
            providedKey.ImportSubjectPublicKeyInfo(providedSpki, out var consumed);
            RsaSigningKeyMaterial.ValidateStrength(providedKey);
            if (consumed != providedSpki.Length) throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            // Only a server-generated, purpose-separated random message; no caller signing oracle.
            var prefix = Encoding.ASCII.GetBytes("argin:local-key-match:v1|");
            var challenge = new byte[prefix.Length + 32];
            prefix.CopyTo(challenge, 0);
            RandomNumberGenerator.Fill(challenge.AsSpan(prefix.Length));
            var signature = privateKey.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            try
            {
                var verified = providedKey.VerifyData(challenge, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return verified && derived.AsSpan().SequenceEqual(providedSpki);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(signature);
                CryptographicOperations.ZeroMemory(challenge);
            }
        }
        catch (CryptographicException) { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
    }
}

internal sealed class VaultKeyMatchProof(IProtectedKeyEnvelopeStore store, KeyEnvelopeProtector protector) : IKeyMatchProof
{
    public async Task<bool> ProveAsync(KeyMatchTicket ticket, byte[] providedSpki, CancellationToken ct)
    {
        var stored = await store.FindAsync(ticket.Identity, ct);
        if (stored is null) throw new KeyProtectionException(KeyProtectionFailure.RecoveryRequired);
        using var clear = await protector.UnprotectAsync(ticket.Identity, stored.Envelope, ct);
        ct.ThrowIfCancellationRequested();
        return RsaKeyMatchProof.Match(clear.Bytes, ticket.StoredSpki, providedSpki);
    }
}
