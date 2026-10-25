using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10LegacyAccessTokenClientTests
{
    [Fact]
    public async Task Requests_token_with_legacy_packet_and_never_returns_private_material()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKey();
        var privateKeyText = Convert.ToBase64String(privateKey);
        using var key = SensitiveBytes.TakeOwnership(privateKey);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["SandboxInvoiceLegacy:Enabled"] = "true",
            ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "true",
            ["SandboxInvoiceLegacy:ApiBaseUrl"] = "https://sandboxrc.tax.gov.ir/req/api/"
        }).Build();
        string? requestBody = null;
        string? requestAuthorization = null;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            requestAuthorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errors\":[],\"result\":{\"data\":{\"token\":\"token-123\"}}}", Encoding.UTF8, "application/json")
            };
        }));
        var provider = new LegacyInvoiceAccessTokenClient(config, () => client);

        var result = await provider.RequestAsync("ABC123", key, rsa.ExportSubjectPublicKeyInfo(), CancellationToken.None);

        Assert.True(result.Authenticated);
        Assert.Equal("token-123", result.AccessToken);
        Assert.Null(requestAuthorization);
        Assert.NotNull(requestBody);
        Assert.Contains("GET_TOKEN", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain(privateKeyText, requestBody, StringComparison.Ordinal);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
