using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Import;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningKeyImportTests
{
    [Theory]
    [InlineData(SigningPrivateKeyFormat.Pkcs8Der)]
    [InlineData(SigningPrivateKeyFormat.Pkcs8Pem)]
    [InlineData(SigningPrivateKeyFormat.EncryptedPkcs8Der)]
    [InlineData(SigningPrivateKeyFormat.EncryptedPkcs8Pem)]
    public void Supported_formats_import_same_public_key(SigningPrivateKeyFormat format)
    {
        using var rsa = RSA.Create(2048);
        using var input = Input(rsa, format);
        using var parsed = new Pkcs8SigningKeyParser().Parse(input);
        Assert.Equal(rsa.ExportSubjectPublicKeyInfo(), parsed.PublicSpkiDer);
        using var imported = RSA.Create();
        imported.ImportPkcs8PrivateKey(parsed.PrivateMaterial.Bytes.Span, out var used);
        Assert.Equal(parsed.PrivateMaterial.Bytes.Length, used);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("pkcs1")]
    [InlineData("extra")]
    [InlineData("two")]
    [InlineData("prefix")]
    [InlineData("password")]
    [InlineData("malformed")]
    public void Invalid_or_wrong_input_is_rejected_without_secret_exception(string kind)
    {
        using var rsa = RSA.Create(2048);
        byte[] bytes = kind switch
        {
            "public" => Encoding.ASCII.GetBytes(rsa.ExportSubjectPublicKeyInfoPem()),
            "pkcs1" => Encoding.ASCII.GetBytes(rsa.ExportRSAPrivateKeyPem()),
            "two" => Encoding.ASCII.GetBytes(rsa.ExportPkcs8PrivateKeyPem() + rsa.ExportPkcs8PrivateKeyPem()),
            "prefix" => Encoding.ASCII.GetBytes("secret-canary" + rsa.ExportPkcs8PrivateKeyPem()),
            "password" => rsa.ExportEncryptedPkcs8PrivateKey("correct", new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)),
            "extra" => rsa.ExportPkcs8PrivateKey().Concat(new byte[] { 0 }).ToArray(),
            _ => Encoding.ASCII.GetBytes("secret-canary")
        };
        var format = kind == "password" ? SigningPrivateKeyFormat.EncryptedPkcs8Der :
            kind == "extra" ? SigningPrivateKeyFormat.Pkcs8Der : SigningPrivateKeyFormat.Pkcs8Pem;
        using var input = new SensitiveKeyInput(format, bytes, kind == "password" ? "wrong" : "");
        var error = Assert.Throws<KeyImportException>(() => new Pkcs8SigningKeyParser().Parse(input));
        Assert.DoesNotContain("secret-canary", error.ToString());
        Assert.Null(error.InnerException);
        CryptographicOperations.ZeroMemory(bytes);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(3072)]
    public void Wrong_rsa_strength_is_rejected(int bits)
    {
        using var rsa = RSA.Create(bits);
        using var input = Input(rsa, SigningPrivateKeyFormat.Pkcs8Der);
        Assert.Throws<KeyImportException>(() => new Pkcs8SigningKeyParser().Parse(input));
    }

    [Fact]
    public void Kdf_iteration_limit_is_checked_before_password_decryption()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.113549.1.5.13");
                using (writer.PushSequence())
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier("1.2.840.113549.1.5.12");
                        using (writer.PushSequence())
                        {
                            writer.WriteOctetString(new byte[16]);
                            writer.WriteInteger(600001);
                            using (writer.PushSequence())
                            {
                                writer.WriteObjectIdentifier("1.2.840.113549.2.9");
                                writer.WriteNull();
                            }
                        }
                    }
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier("2.16.840.1.101.3.4.1.42");
                        writer.WriteOctetString(new byte[16]);
                    }
                }
            }
            writer.WriteOctetString(new byte[16]);
        }
        using var input = new SensitiveKeyInput(SigningPrivateKeyFormat.EncryptedPkcs8Der, writer.Encode(), "password");
        Assert.Equal(KeyImportFailure.UnsupportedProtection,
            Assert.Throws<KeyImportException>(() => new Pkcs8SigningKeyParser().Parse(input)).Failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Encrypted_inner_trailing_data_or_non_der_length_is_rejected(bool nonDer)
    {
        using var rsa = RSA.Create(2048);
        var valid = rsa.ExportPkcs8PrivateKey();
        byte[] malformed;
        if (nonDer)
        {
            // RSA2048 DER starts30 82 LL LL. Nonminimal BER length30 83 00 LL LL is not DER.
            Assert.Equal((byte)0x82, valid[1]);
            malformed = new byte[valid.Length + 1];
            malformed[0] = 0x30;
            malformed[1] = 0x83;
            malformed[2] = 0;
            valid.AsSpan(2).CopyTo(malformed.AsSpan(3));
        }
        else malformed = valid.Concat(new byte[] { 0 }).ToArray();
        var salt = new byte[16];
        var iv = new byte[16];
        var dek = Rfc2898DeriveBytes.Pbkdf2("password", salt, 1000, HashAlgorithmName.SHA256, 32);
        try
        {
            using var aes = Aes.Create();
            aes.Key = dek;
            var encrypted = aes.EncryptCbc(malformed, iv, PaddingMode.PKCS7);
            var writer = new AsnWriter(AsnEncodingRules.DER);
            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier("1.2.840.113549.1.5.13");
                    using (writer.PushSequence())
                    {
                        using (writer.PushSequence())
                        {
                            writer.WriteObjectIdentifier("1.2.840.113549.1.5.12");
                            using (writer.PushSequence())
                            {
                                writer.WriteOctetString(salt);
                                writer.WriteInteger(1000);
                                using (writer.PushSequence())
                                {
                                    writer.WriteObjectIdentifier("1.2.840.113549.2.9");
                                    writer.WriteNull();
                                }
                            }
                        }
                        using (writer.PushSequence())
                        {
                            writer.WriteObjectIdentifier("2.16.840.1.101.3.4.1.42");
                            writer.WriteOctetString(iv);
                        }
                    }
                }
                writer.WriteOctetString(encrypted);
            }
            using var input = new SensitiveKeyInput(SigningPrivateKeyFormat.EncryptedPkcs8Der, writer.Encode(), "password");
            Assert.Throws<KeyImportException>(() => new Pkcs8SigningKeyParser().Parse(input));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(valid);
            CryptographicOperations.ZeroMemory(malformed);
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    [Fact]
    public void Sensitive_input_is_bounded_nonserializable_and_unusable_after_disposal()
    {
        using var input = new SensitiveKeyInput(SigningPrivateKeyFormat.Pkcs8Der, new byte[] { 1 }, "secret-canary");
        Assert.DoesNotContain("secret-canary", input.ToString());
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(input));
        var raw = input.KeyBytes;
        var password = input.Password;
        input.Dispose();
        Assert.Equal(new byte[] { 0 }, raw.ToArray());
        Assert.All(password.ToArray(), c => Assert.Equal('\0', c));
        Assert.Throws<ObjectDisposedException>(() => input.KeyBytes);
        Assert.Throws<ArgumentException>(() =>
            new SensitiveKeyInput(SigningPrivateKeyFormat.Pkcs8Der, new byte[65537]));
    }

    internal static SensitiveKeyInput Input(RSA rsa, SigningPrivateKeyFormat format)
    {
        var encrypted = format is SigningPrivateKeyFormat.EncryptedPkcs8Der or SigningPrivateKeyFormat.EncryptedPkcs8Pem;
        var bytes = encrypted
            ? rsa.ExportEncryptedPkcs8PrivateKey("test-password", new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000))
            : rsa.ExportPkcs8PrivateKey();
        try
        {
            if (format is SigningPrivateKeyFormat.Pkcs8Pem or SigningPrivateKeyFormat.EncryptedPkcs8Pem)
            {
                var pem = Encoding.ASCII.GetBytes(PemEncoding.WriteString(encrypted ? "ENCRYPTED PRIVATE KEY" : "PRIVATE KEY", bytes));
                try { return new(format, pem, encrypted ? "test-password" : ""); }
                finally { CryptographicOperations.ZeroMemory(pem); }
            }
            return new(format, bytes, encrypted ? "test-password" : "");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
