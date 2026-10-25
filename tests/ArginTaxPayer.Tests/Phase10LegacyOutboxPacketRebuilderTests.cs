using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10LegacyOutboxPacketRebuilderTests
{
    [Fact]
    public void Rebuilds_encrypted_packet_with_fresh_token_and_rejects_plaintext()
    {
        using var rsa = RSA.Create(2048);
        using var authorityRsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var operation = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var taxpayer = Guid.NewGuid();
        using var authority = new LegacySandboxEncryptionKey(
            "authority-test",(byte[])authorityRsa.ExportSubjectPublicKeyInfo().Clone());
        var initial = LegacyInvoiceSubmissionPacketBuilder.BuildEncrypted(
            operation,customer,taxpayer,"AAAAAAAAAAAAAAAAAAAAAA",
            "{\\"header\\":{\\"taxid\\":\\"AAAAAAAAAAAAAAAAAAAAAA\\"},\\"body\\":[]}",
            key,rsa.ExportSubjectPublicKeyInfo(),"old-token",
            Guid.NewGuid(),"1700000000000","ABC123",authority);
        var payload = JsonSerializer.Serialize(new {
            schema = "phase10.sandbox.legacy.invoice.dispatch.v1",
            environment = "Sandbox",operationId=operation,
            customerUserId=customer,taxpayerId=taxpayer,
            fiscalTaxId="AAAAAAAAAAAAAAAAAAAAAA",
            requestJson=initial.RequestJson,
            transferSignature=initial.TransferSignature,
            requestSha256=initial.RequestSha256,accessTokenStored=false
        });
        var rebuilt=LegacyInvoiceOutboxPacketRebuilder.Rebuild(payload,key,
            rsa.ExportSubjectPublicKeyInfo(),"new-token",
            sandboxMemoryClientId:"ABC123");
        Assert.NotEqual(initial.TransferSignature,rebuilt.TransferSignature);
        using var original=JsonDocument.Parse(initial.RequestJson);
        using var replayed=JsonDocument.Parse(rebuilt.RequestJson);
        Assert.Equal(
            original.RootElement.GetProperty("packets")[0].GetProperty("data").GetString(),
            replayed.RootElement.GetProperty("packets")[0].GetProperty("data").GetString());
        Assert.Equal("INVOICE.V01",replayed.RootElement.GetProperty("packets")[0]
            .GetProperty("packetType").GetString());
        var bad=payload.Replace("false","true",StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(()=>LegacyInvoiceOutboxPacketRebuilder.Rebuild(
            bad,key,rsa.ExportSubjectPublicKeyInfo(),"new-token",
            sandboxMemoryClientId:"ABC123"));
        var plaintext=LegacyInvoiceSubmissionPacketBuilder.Build(
            operation,customer,taxpayer,"ABC123","{\\"amount\\":125}",
            key,rsa.ExportSubjectPublicKeyInfo(),"old-token",
            Guid.NewGuid(),"1700000000000");
        var oldPayload=JsonSerializer.Serialize(new {
            schema="phase10.sandbox.legacy.invoice.dispatch.v1",
            environment="Sandbox",operationId=operation,customerUserId=customer,
            taxpayerId=taxpayer,fiscalTaxId="ABC123",requestJson=plaintext.RequestJson,
            transferSignature=plaintext.TransferSignature,
            requestSha256=plaintext.RequestSha256,accessTokenStored=false
        });
        Assert.Throws<ArgumentException>(()=>LegacyInvoiceOutboxPacketRebuilder.Rebuild(
            oldPayload,key,rsa.ExportSubjectPublicKeyInfo(),"new-token",
            sandboxMemoryClientId:"ABC123"));
    }

}
