using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

internal sealed class GeneratedRsaKey(SensitiveBytes privateMaterial, byte[] publicSpkiDer) : IDisposable
{
    public SensitiveBytes PrivateMaterial { get; } = privateMaterial;
    public byte[] PublicSpkiDer { get; } = publicSpkiDer;
    public string PublicFingerprintSha256 => RsaSigningKeyMaterial.Fingerprint(PublicSpkiDer);
    public void Dispose() => PrivateMaterial.Dispose();
    public override string ToString() => "[generated signing key]";
}

internal sealed class RsaGeneratedKeyFactory : IGeneratedKeyFactory
{
    public GeneratedRsaKey Create()
    {
        using var rsa = RSA.Create(2048);
        RsaSigningKeyMaterial.ValidateStrength(rsa);
        var publicBytes = rsa.ExportSubjectPublicKeyInfo();
        return new(SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey()), publicBytes);
    }
}

internal static class RsaSigningKeyMaterial
{
    public static string Fingerprint(byte[] spki) =>
        Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();

    public static void ValidateStrength(RSA rsa)
    {
        if (rsa.KeySize != 2048 || rsa.ExportParameters(false).Exponent is not { } exponent ||
            !exponent.AsSpan().SequenceEqual(new byte[] { 1, 0, 1 }))
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
    }

    public static byte[] DerivePublic(ReadOnlyMemory<byte> pkcs8)
    {
        if (pkcs8.Length is < 1 or > KeyEnvelopeProtector.MaximumKeyBytes)
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        byte[]? canonical = null;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(pkcs8.Span, out var consumed);
            ValidateStrength(rsa);
            canonical = rsa.ExportPkcs8PrivateKey();
            if (consumed != pkcs8.Length || !CryptographicOperations.FixedTimeEquals(canonical, pkcs8.Span))
                throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            return rsa.ExportSubjectPublicKeyInfo();
        }
        catch (CryptographicException) { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
        finally { if (canonical is not null) CryptographicOperations.ZeroMemory(canonical); }
    }

    public static SigningPublicKeyArtifact Artifact(Guid keyId, byte[] spki, long revision, short lifecycle)
    {
        if (keyId == Guid.Empty || spki is not { Length: > 0 and <= 4096 } || revision < 1 ||
            lifecycle is < 2 or > 5)
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(spki, out var consumed);
            ValidateStrength(rsa);
            if (consumed != spki.Length || !rsa.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(spki))
                throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            var pem = PemEncoding.WriteString("PUBLIC KEY", spki).Replace("\r\n", "\n").TrimEnd() + "\n";
            return new(keyId, SigningOperationPolicy.LegacyProfile, Fingerprint(spki), pem,
                $"argin-public-key-{keyId:N}.pem", revision, lifecycle);
        }
        catch (CryptographicException) { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
    }
}
