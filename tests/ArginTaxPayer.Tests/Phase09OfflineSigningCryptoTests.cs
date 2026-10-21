using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09OfflineSigningCryptoTests
{
    [Theory]
    [InlineData(LegacySigningMode.ObjectV030)]
    [InlineData(LegacySigningMode.SimpleV1V030)]
    public void Offline_signature_is_sha256_pkcs1_v15_utf8_and_ordinary_base64(LegacySigningMode mode)
    {
        using var rsa = RSA.Create(2048);
        using var material = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var fixture = new { Value = "محتوای آزمون#" };
        var engine = new LegacyOfflineSigningEngine();
        LegacyOfflineSignatureResult result;
        byte[] bytes;
        if (mode == LegacySigningMode.ObjectV030)
        {
            using var normalized = LegacyPayloadNormalizer.NormalizeObjectPayload(fixture, LegacySigningPurpose.Content);
            bytes = normalized.Bytes.ToArray();
            result = engine.SignObject(material, rsa.ExportSubjectPublicKeyInfo(), fixture,
                LegacySigningPurpose.Content);
        }
        else
        {
            var payload = LegacyPayloadNormalizer.SerializePayload(fixture, mode);
            bytes = payload;
            result = engine.Sign(material, rsa.ExportSubjectPublicKeyInfo(), payload, mode,
                LegacySigningPurpose.Content);
        }
        var expected = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.Equal(Convert.ToBase64String(expected), result.SignatureBase64);
        Assert.Equal(Convert.ToBase64String(Convert.FromBase64String(result.SignatureBase64)), result.SignatureBase64);
        Assert.Equal(2048 / 8, Convert.FromBase64String(result.SignatureBase64).Length);
        Assert.True(rsa.VerifyData(bytes, Convert.FromBase64String(result.SignatureBase64), HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(3072)]
    public void Unsupported_rsa_strength_is_rejected(int bits)
    {
        using var rsa = RSA.Create(bits);
        using var material = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        Assert.Throws<KeyProtectionException>(() => new LegacyOfflineSigningEngine().Sign(material,
            rsa.ExportSubjectPublicKeyInfo(), "{}"u8.ToArray(), LegacySigningMode.SimpleV1V030,
            LegacySigningPurpose.Content));
    }

    [Fact]
    public void Stored_public_key_must_match_private_material_and_is_never_replaced()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        using var material = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        Assert.Throws<KeyProtectionException>(() => new LegacyOfflineSigningEngine().Sign(material,
            other.ExportSubjectPublicKeyInfo(), "{}"u8.ToArray(), LegacySigningMode.SimpleV1V030,
            LegacySigningPurpose.Content));
    }

    [Fact]
    public void Invalid_or_trailing_private_key_data_is_rejected()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKey();
        using var material = SensitiveBytes.TakeOwnership(privateKey.Concat(new byte[] { 0 }).ToArray());
        Assert.Throws<KeyProtectionException>(() => new LegacyOfflineSigningEngine().Sign(material,
            rsa.ExportSubjectPublicKeyInfo(), "{}"u8.ToArray(), LegacySigningMode.SimpleV1V030,
            LegacySigningPurpose.Content));
    }
}
