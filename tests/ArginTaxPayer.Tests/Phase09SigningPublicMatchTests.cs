using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Matching;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningPublicMatchTests
{
    [Theory]
    [InlineData(SigningPublicKeyFormat.SpkiDer)]
    [InlineData(SigningPublicKeyFormat.SpkiPem)]
    [InlineData(SigningPublicKeyFormat.RsaPublicDer)]
    [InlineData(SigningPublicKeyFormat.RsaPublicPem)]
    public void Supported_public_formats_have_same_canonical_spki_and_fingerprint(SigningPublicKeyFormat format)
    {
        using var rsa = RSA.Create(2048);
        var parsed = new SigningPublicKeyParser().Parse(format, PublicInput(rsa, format));
        Assert.Equal(rsa.ExportSubjectPublicKeyInfo(), parsed);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant(),
            ArginTaxPayer.Infrastructure.Signing.Generation.RsaSigningKeyMaterial.Fingerprint(parsed));
    }

    [Theory]
    [InlineData("trailing")]
    [InlineData("ber")]
    [InlineData("two")]
    [InlineData("prefix")]
    [InlineData("private")]
    [InlineData("wrong-label")]
    [InlineData("oversize")]
    [InlineData("unknown")]
    public void Ambiguous_private_or_malformed_public_input_is_rejected(string kind)
    {
        using var rsa = RSA.Create(2048);
        var der = rsa.ExportSubjectPublicKeyInfo();
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        var bytes = kind switch
        {
            "trailing" => der.Concat(new byte[] { 0 }).ToArray(),
            "ber" => new byte[] { 0x30, 0x83, 0 }.Concat(der.AsSpan(2).ToArray()).ToArray(),
            "two" => Encoding.ASCII.GetBytes(pem + pem),
            "prefix" => Encoding.ASCII.GetBytes("canary" + pem),
            "private" => Encoding.ASCII.GetBytes(rsa.ExportPkcs8PrivateKeyPem()),
            "wrong-label" => Encoding.ASCII.GetBytes(rsa.ExportRSAPublicKeyPem()),
            "oversize" => new byte[16385],
            _ => der
        };
        var format = kind is "trailing" or "ber" or "oversize" ? SigningPublicKeyFormat.SpkiDer :
            kind == "unknown" ? (SigningPublicKeyFormat)99 : SigningPublicKeyFormat.SpkiPem;
        try
        {
            var error = Assert.Throws<KeyProtectionException>(() => new SigningPublicKeyParser().Parse(format, bytes));
            Assert.DoesNotContain("canary", error.ToString());
            Assert.Null(error.InnerException);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(3072)]
    public void Unsupported_strength_is_rejected(int bits)
    {
        using var rsa = RSA.Create(bits);
        Assert.Throws<KeyProtectionException>(() => new SigningPublicKeyParser().Parse(
            SigningPublicKeyFormat.SpkiDer, rsa.ExportSubjectPublicKeyInfo()));
    }

    [Fact]
    public void Unsupported_public_exponent_is_rejected()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(false);
        parameters.Exponent = new byte[] { 3 };
        using var otherExponent = RSA.Create();
        otherExponent.ImportParameters(parameters);
        Assert.Throws<KeyProtectionException>(() => new SigningPublicKeyParser().Parse(
            SigningPublicKeyFormat.SpkiDer, otherExponent.ExportSubjectPublicKeyInfo()));
    }

    [Fact]
    public void Proof_matches_same_key_and_reports_different_provided_key()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        using var clear = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        Assert.True(RsaKeyMatchProof.Match(clear.Bytes, rsa.ExportSubjectPublicKeyInfo(), rsa.ExportSubjectPublicKeyInfo()));
        Assert.False(RsaKeyMatchProof.Match(clear.Bytes, rsa.ExportSubjectPublicKeyInfo(), other.ExportSubjectPublicKeyInfo()));
    }

    [Fact]
    public void Stored_public_metadata_must_match_protected_private_key()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        using var clear = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        Assert.Throws<KeyProtectionException>(() => RsaKeyMatchProof.Match(clear.Bytes,
            other.ExportSubjectPublicKeyInfo(), other.ExportSubjectPublicKeyInfo()));
    }

    internal static byte[] PublicInput(RSA rsa, SigningPublicKeyFormat format) => format switch
    {
        SigningPublicKeyFormat.SpkiDer => rsa.ExportSubjectPublicKeyInfo(),
        SigningPublicKeyFormat.SpkiPem => Encoding.ASCII.GetBytes(rsa.ExportSubjectPublicKeyInfoPem()),
        SigningPublicKeyFormat.RsaPublicDer => rsa.ExportRSAPublicKey(),
        _ => Encoding.ASCII.GetBytes(rsa.ExportRSAPublicKeyPem())
    };
}
