using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Offline;

internal sealed record LegacyOfflineSignatureResult(string SignatureBase64, string ProfileId);
internal interface ILegacyOfflineSigningEngine
{
    LegacyOfflineSignatureResult Sign(SensitiveBytes privateMaterial, ReadOnlyMemory<byte> expectedPublicSpki,
        ReadOnlyMemory<byte> payloadJson, LegacySigningMode mode, LegacySigningPurpose purpose,
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyDictionary<string, string>? customHeaders = null, string tokenHeaderName = "Authorization");

    LegacyOfflineSignatureResult SignObject(SensitiveBytes privateMaterial, ReadOnlyMemory<byte> expectedPublicSpki,
        object payload, LegacySigningPurpose purpose, IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyDictionary<string, string>? customHeaders = null, string tokenHeaderName = "Authorization");
}

internal sealed class LegacyOfflineSigningEngine : ILegacyOfflineSigningEngine
{
    public LegacyOfflineSignatureResult Sign(SensitiveBytes privateMaterial, ReadOnlyMemory<byte> expectedPublicSpki,
        ReadOnlyMemory<byte> payloadJson, LegacySigningMode mode, LegacySigningPurpose purpose,
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyDictionary<string, string>? customHeaders = null, string tokenHeaderName = "Authorization")
    {
        ArgumentNullException.ThrowIfNull(privateMaterial);
        using var normalized = LegacyPayloadNormalizer.Normalize(payloadJson, mode, purpose, headers,
            customHeaders, tokenHeaderName);
        return SignNormalized(privateMaterial, expectedPublicSpki, normalized,
            mode == LegacySigningMode.ObjectV030
                ? "legacy-key-source-0.0.30-object-rsa-sha256-pkcs1-base64"
                : "legacy-key-source-0.0.30-simple-v1-rsa-sha256-pkcs1-base64");
    }

    public LegacyOfflineSignatureResult SignObject(SensitiveBytes privateMaterial,
        ReadOnlyMemory<byte> expectedPublicSpki, object payload, LegacySigningPurpose purpose,
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyDictionary<string, string>? customHeaders = null, string tokenHeaderName = "Authorization")
    {
        ArgumentNullException.ThrowIfNull(privateMaterial);
        using var normalized = LegacyPayloadNormalizer.NormalizeObjectPayload(payload, purpose,
            headers, customHeaders, tokenHeaderName);
        return SignNormalized(privateMaterial, expectedPublicSpki, normalized,
            "legacy-key-source-0.0.30-object-rsa-sha256-pkcs1-base64");
    }

    private static LegacyOfflineSignatureResult SignNormalized(SensitiveBytes privateMaterial,
        ReadOnlyMemory<byte> expectedPublicSpki, SensitiveBytes normalized, string profile)
    {
        byte[]? signature = null;
        byte[]? derivedSpki = null;
        try
        {
            if (expectedPublicSpki.Length is < 1 or > 4096) throw Integrity();
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateMaterial.Bytes.Span, out var consumed);
            if (consumed != privateMaterial.Bytes.Length) throw Integrity();
            RsaSigningKeyMaterial.ValidateStrength(rsa);
            derivedSpki = rsa.ExportSubjectPublicKeyInfo();
            if (derivedSpki.Length != expectedPublicSpki.Length ||
                !CryptographicOperations.FixedTimeEquals(derivedSpki, expectedPublicSpki.Span)) throw Integrity();
            signature = rsa.SignData(normalized.Bytes.Span, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return new(Convert.ToBase64String(signature), profile);
        }
        catch (LegacySigningInputException) { throw; }
        catch (Exception error) when (error is CryptographicException or ArgumentException or KeyProtectionException)
        { throw Integrity(); }
        finally
        {
            if (signature is not null) CryptographicOperations.ZeroMemory(signature);
            if (derivedSpki is not null) CryptographicOperations.ZeroMemory(derivedSpki);
        }
    }
    private static KeyProtectionException Integrity() => new(KeyProtectionFailure.Integrity);
}
