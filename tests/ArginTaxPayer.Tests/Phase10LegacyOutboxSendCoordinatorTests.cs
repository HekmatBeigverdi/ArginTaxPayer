using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10LegacyOutboxSendCoordinatorTests
{
    [Fact]
    public async Task Gets_fresh_token_rebuilds_packet_and_sends_once()
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
            schema = "phase10.sandbox.legacy.invoice.dispatch.v1", environment = "Sandbox",
            operationId = operation, customerUserId = customer, taxpayerId = taxpayer,
            fiscalTaxId = "ABC123", requestJson = initial.RequestJson,
            transferSignature = initial.TransferSignature, requestSha256 = initial.RequestSha256,
            accessTokenStored = false
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["SandboxInvoiceLegacy:Enabled"] = "true",
            ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "true",
            ["SandboxInvoiceLegacy:ApiBaseUrl"] = "https://sandboxrc.tax.gov.ir/req/api/"
        }).Build();
        var invoiceRequests = 0;
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("GET_TOKEN", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK,
                    "{\"errors\":[],\"result\":{\"data\":{\"token\":\"fresh-token\"}}}");
            invoiceRequests++;
            Assert.Equal("Bearer fresh-token", request.Headers.Authorization?.ToString());
            return Json(HttpStatusCode.OK,
                "{\"errors\":null,\"result\":[{\"referenceNumber\":\"REF-1\"}]}");
        });
        var coordinator = new LegacyInvoiceOutboxSendCoordinator(
            new LegacyInvoiceAccessTokenClient(config,
                () => new HttpClient(handler, disposeHandler: false)),
            new LegacyInvoiceSubmissionHttpTransport(config,
                () => new HttpClient(handler, disposeHandler: false)));
        using var signingKey = new DatabaseLegacySigningKey(key, rsa.ExportSubjectPublicKeyInfo());

        var result = await coordinator.SendAsync(payload, signingKey);

        Assert.True(result.Acknowledged);
        Assert.Equal("REF-1", result.ReferenceNumber);
        Assert.Equal(1, invoiceRequests);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
