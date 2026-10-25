using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;
using Xunit;
namespace ArginTaxPayer.Tests;
public sealed class Phase10SandboxInvoicePacketCryptographyTests
{
    private static byte[] Decode(string part)
    {
        string str=part.Replace('-','+').Replace('_','/');
        return Convert.FromBase64String(str.PadRight((str.Length+3)/4*4,'='));
    }

    [Fact]
    public void Jws_signature_verifies_and_jwe_roundtrips_exact_signed_bytes()
    {
        using var signer=RSA.Create(2048);
        var req=new CertificateRequest("CN=Sandbox offline fixture",
            signer,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var fixtureCertificate=req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        var invoice="{\"header\":{\"taxid\":\"ABC1230644600000000017\"},\"body\":[]}";
        string jws=Phase10SandboxInvoicePacketCryptography.SignJws(
            invoice,signer,fixtureCertificate,DateTime.UtcNow);
        string[] jwsParts=jws.Split('.');
        Assert.Equal(3,jwsParts.Length);
        Assert.True(signer.VerifyData(Encoding.ASCII.GetBytes(
            jwsParts[0]+"."+jwsParts[1]),Decode(jwsParts[2]),
            HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
        Assert.Equal(invoice,Encoding.UTF8.GetString(Decode(jwsParts[1])));

        using var authority=RSA.Create(2048);
        var encrypted=Phase10SandboxInvoicePacketCryptography.EncryptJwe(
            jws,authority,"test-key-id");
        var parts=encrypted.Split('.');
        Assert.Equal(5,parts.Length);
        using var header=JsonDocument.Parse(Decode(parts[0]));
        Assert.Equal("RSA-OAEP-256",header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("A256GCM",header.RootElement.GetProperty("enc").GetString());
        Assert.Equal("test-key-id",header.RootElement.GetProperty("kid").GetString());
        byte[] key=authority.Decrypt(Decode(parts[1]),RSAEncryptionPadding.OaepSHA256);
        byte[] plaintext=new byte[Decode(parts[3]).Length];
        using(var aes=new AesGcm(key,16))
            aes.Decrypt(Decode(parts[2]),Decode(parts[3]),Decode(parts[4]),plaintext,
                Encoding.ASCII.GetBytes(parts[0]));
        Assert.Equal(jws,Encoding.UTF8.GetString(plaintext));
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(plaintext);
    }

    [Fact]
    public void Wrong_certificate_cannot_sign_invoice()
    {
        using var signer=RSA.Create(2048);
        using var another=RSA.Create(2048);
        var request=new CertificateRequest("CN=Unrelated fixture",
            another,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        Assert.Throws<CryptographicException>(()=>
            Phase10SandboxInvoicePacketCryptography.SignJws(
                "{}",signer,cert,DateTime.UtcNow));
    }
}
