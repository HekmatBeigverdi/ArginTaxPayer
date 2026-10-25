using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10LegacyOutboxPacketRebuilderTests
{
    [Fact]
    public void Rebuilds_with_fresh_token_and_rejects_stored_token()
    {
        using var rsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var operation = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var taxpayer = Guid.NewGuid();
        var initial = LegacyInvoiceSubmissionPacketBuilder.Build(operation, customer, taxpayer,
            "ABC123", "{\"amount\":125}", key, rsa.ExportSubjectPublicKeyInfo(),
            "old-token", Guid.NewGuid(), "1700000000000");
        var payload = JsonSerializer.Serialize(new
        {
            schema = "phase10.sandbox.legacy.invoice.dispatch.v1",
            environment = "Sandbox", operationId = operation, customerUserId = customer,
            taxpayerId = taxpayer, fiscalTaxId = "ABC123", requestJson = initial.RequestJson,
            transferSignature = initial.TransferSignature,
            requestSha256 = initial.RequestSha256, accessTokenStored = false
        });

        var rebuilt = LegacyInvoiceOutboxPacketRebuilder.Rebuild(payload, key,
            rsa.ExportSubjectPublicKeyInfo(), "new-token");
        Assert.NotEqual(initial.TransferSignature, rebuilt.TransferSignature);
        Assert.Contains("125", rebuilt.RequestJson, StringComparison.Ordinal);

        var bad = payload.Replace("false", "true", StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => LegacyInvoiceOutboxPacketRebuilder.Rebuild(
            bad, key, rsa.ExportSubjectPublicKeyInfo(), "new-token"));
    }
}
