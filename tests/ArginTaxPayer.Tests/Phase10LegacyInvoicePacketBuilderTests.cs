using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10LegacyInvoicePacketBuilderTests
{
    [Fact]
    public void Encrypted_content_signature_covers_invoice_values_not_JsonElement_metadata()
    {
        using var rsa = RSA.Create(2048);
        using var authorityRsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        using var authority = new LegacySandboxEncryptionKey("authority-test", authorityRsa.ExportSubjectPublicKeyInfo());
        var packet = LegacyInvoiceSubmissionPacketBuilder.BuildEncrypted(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "AAAAAAAAAAAAAAAAAAAAAA",
            """{"header":{"taxid":"AAAAAAAAAAAAAAAAAAAAAA"},"body":[{"sstid":"1234567890123","am":2,"fee":100}]}""",
            key, rsa.ExportSubjectPublicKeyInfo(), "token-value", Guid.NewGuid(), "1792939200000", "ABC123", authority);
        using var document = JsonDocument.Parse(packet.RequestJson);
        var signature = document.RootElement.GetProperty("packets")[0].GetProperty("dataSignature").GetString()!;
        // TaxPayerBackend ObjectNormalizer: sorted body fields followed by header.taxid.
        Assert.True(rsa.VerifyData(Encoding.UTF8.GetBytes("2#100#1234567890123#AAAAAAAAAAAAAAAAAAAAAA"),
            Convert.FromBase64String(signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Async_transfer_signature_matches_legacy_packet_collection_without_signature_key_id(bool replay)
    {
        using var rsa = RSA.Create(2048);
        using var authorityRsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        using var authority = new LegacySandboxEncryptionKey("authority-test", authorityRsa.ExportSubjectPublicKeyInfo());
        var packet = LegacyInvoiceSubmissionPacketBuilder.BuildEncrypted(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "AAAAAAAAAAAAAAAAAAAAAA",
            """{"header":{"taxid":"AAAAAAAAAAAAAAAAAAAAAA"},"body":[]}""",
            key, rsa.ExportSubjectPublicKeyInfo(), "token-value", Guid.NewGuid(), "1792939200000", "ABC123", authority);
        if (replay)
        {
            using var initial = JsonDocument.Parse(packet.RequestJson);
            var p = initial.RootElement.GetProperty("packets")[0];
            packet = LegacyInvoiceSubmissionPacketBuilder.ReSignEncrypted(
                packet.OperationId, packet.CustomerUserId, packet.TaxpayerId, packet.FiscalTaxId,
                p.GetProperty("data").GetString()!, p.GetProperty("encryptionKeyId").GetString()!,
                p.GetProperty("symmetricKey").GetString()!, p.GetProperty("iv").GetString()!,
                p.GetProperty("dataSignature").GetString()!, "ABC123", key, rsa.ExportSubjectPublicKeyInfo(),
                "token-value", Guid.NewGuid(), "1792939200001");
        }
        using var document = JsonDocument.Parse(packet.RequestJson);
        var sent = document.RootElement.GetProperty("packets")[0];
        // Independent SDK field ordering: Authorization, Packets.E0.*, requestTraceId, timestamp.
        var values = new[] { "token-value", sent.GetProperty("data").GetString(),
            sent.GetProperty("dataSignature").GetString(), sent.GetProperty("encryptionKeyId").GetString(),
            "ABC123", sent.GetProperty("iv").GetString(), "INVOICE.V01", "false",
            sent.GetProperty("symmetricKey").GetString(), packet.OperationId.ToString("D"),
            packet.RequestTraceId, packet.Timestamp };
        Assert.True(rsa.VerifyData(Encoding.UTF8.GetBytes(string.Join('#', values)),
            Convert.FromBase64String(packet.TransferSignature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Builds_legacy_request_with_signature_bound_to_exact_packet()
    {
        using var rsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var trace = Guid.NewGuid();
        var timestamp = "1792939200000";
        var result = LegacyInvoiceSubmissionPacketBuilder.Build(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ABC1230000000000000001",
            "{\"header\":{\"taxid\":\"ABC1230000000000000001\"},\"body\":[]}",
            key, rsa.ExportSubjectPublicKeyInfo(), "token-value", trace, timestamp);

        using var request = JsonDocument.Parse(result.RequestJson);
        var root = request.RootElement;
        Assert.Equal(result.TransferSignature, root.GetProperty("signature").GetString());
        Assert.Single(root.GetProperty("packets").EnumerateArray());
        Assert.Equal(trace.ToString("D"), result.RequestTraceId);
        Assert.DoesNotContain("PRIVATE KEY", result.RequestJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Legacy_invoice_packet_uses_client_memory_code_not_22_character_tax_id()
    {
        using var rsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var packet = LegacyInvoiceSubmissionPacketBuilder.Build(
            Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            "AAAAAAAAAAAAAAAAAAAAAA",
            """{"header":{"taxid":"AAAAAAAAAAAAAAAAAAAAAA"},"body":[]}""",
            key,rsa.ExportSubjectPublicKeyInfo(),"token-value",
            Guid.NewGuid(),"1792939200000","ABC123");
        using var doc = JsonDocument.Parse(packet.RequestJson);
        var sent = doc.RootElement.GetProperty("packets")[0];
        Assert.Equal("INVOICE.V01",sent.GetProperty("packetType").GetString());
        Assert.Equal("ABC123",sent.GetProperty("fiscalId").GetString());
        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAA",packet.FiscalTaxId);
    }

    [Fact]
    public void Rejects_empty_invoice_payload_or_invalid_scope()
    {
        using var rsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        Assert.Throws<ArgumentException>(() => LegacyInvoiceSubmissionPacketBuilder.Build(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "ABC1230000000000000001", "{}", key,
            rsa.ExportSubjectPublicKeyInfo(), "token", Guid.NewGuid(), "1792939200000"));
    }
}
