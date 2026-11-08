using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18LegacyOutboxPacketEnvironmentTests
{
    [Fact]
    public void Production_encrypted_packet_can_only_be_resigned_under_production_environment()
    {
        using var rsa=RSA.Create(2048);
        using var authorityRsa=RSA.Create(2048);
        using var key=SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        using var authority=new LegacySandboxEncryptionKey(
            "authority-test",(byte[])authorityRsa.ExportSubjectPublicKeyInfo().Clone());
        var operation=Guid.NewGuid();
        var original=LegacyInvoiceSubmissionPacketBuilder.BuildEncrypted(
            operation,Guid.NewGuid(),Guid.NewGuid(),"AAAAAAAAAAAAAAAAAAAAAA",
            """{"header":{"taxid":"AAAAAAAAAAAAAAAAAAAAAA"},"body":[]}""",
            key,rsa.ExportSubjectPublicKeyInfo(),"original-token",
            Guid.NewGuid(),"1700000000000","ABC123",authority);
        var payload=JsonSerializer.Serialize(new {
            schema="phase18.production.legacy.invoice.dispatch.v1",
            environment="Production",
            operationId=original.OperationId,
            customerUserId=original.CustomerUserId,
            taxpayerId=original.TaxpayerId,
            fiscalTaxId=original.FiscalTaxId,
            requestJson=original.RequestJson,
            transferSignature=original.TransferSignature,
            requestSha256=original.RequestSha256,
            accessTokenStored=false
        });
        var rebuilt=LegacyInvoiceOutboxPacketRebuilder.Rebuild(
            payload,key,rsa.ExportSubjectPublicKeyInfo(),"new-token",
            sandboxMemoryClientId:"ABC123",
            environment:TaxAuthorityEnvironment.Production);
        Assert.NotEqual(original.TransferSignature,rebuilt.TransferSignature);
        Assert.Equal(operation,rebuilt.OperationId);
        Assert.Throws<ArgumentException>(()=>LegacyInvoiceOutboxPacketRebuilder.Rebuild(
            payload,key,rsa.ExportSubjectPublicKeyInfo(),"new-token",
            sandboxMemoryClientId:"ABC123"));
        Assert.Throws<ArgumentException>(()=>LegacyInvoiceOutboxPacketRebuilder.Rebuild(
            payload.Replace("\"Production\"","\"Sandbox\"",StringComparison.Ordinal),
            key,rsa.ExportSubjectPublicKeyInfo(),"new-token",
            sandboxMemoryClientId:"ABC123",
            environment:TaxAuthorityEnvironment.Production));
    }
}
