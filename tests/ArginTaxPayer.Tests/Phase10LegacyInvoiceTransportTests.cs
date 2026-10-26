using System.Net;
using System.Text;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10LegacyInvoiceTransportTests
{
    private static IConfiguration Config(string url = "https://sandboxrc.tax.gov.ir/req/api/") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SandboxInvoiceLegacy:Enabled"] = "true",
            ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "true",
            ["SandboxInvoiceLegacy:ApiBaseUrl"] = url
        }).Build();

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Calls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string TestUid = "c7251456-d883-4e06-8ae9-8aa5f62e238c";
    private static string ValidRequest(string signature) =>
        "{\"signature\":\""+signature+"\",\"packets\":[{\"uid\":\""+TestUid+"\"}]}";

    [Fact]
    public async Task Posts_legacy_async_enqueue_to_the_pinned_sandbox_route()
    {
        var handler = new FakeHandler(_ => Json(
            """{"timestamp":1,"result":[{"uid":"c7251456-d883-4e06-8ae9-8aa5f62e238c","referenceNumber":"r-1","errorCode":null,"errorDetail":null}],"errors":[]}"""));
        var transport = new LegacyInvoiceSubmissionHttpTransport(
            Config(), () => new HttpClient(handler, disposeHandler: false));

        var result = await transport.PostAsync(
            "A2HPH4", ValidRequest(new string('A', 344)), new string('A', 344),
            "token", Guid.NewGuid().ToString("D"), "123", default);

        Assert.True(result.Acknowledged);
        Assert.Equal("r-1", result.ReferenceNumber);
        var request = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://sandboxrc.tax.gov.ir/req/api/self-tsp/async/normal-enqueue",
            request.RequestUri!.ToString());
        Assert.Equal("Bearer token", request.Headers.Authorization!.ToString());
        Assert.Equal("123", request.Headers.GetValues("timestamp").Single());
        Assert.DoesNotContain("signature", request.Headers.Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://tp.tax.gov.ir/req/api/")]
    [InlineData("http://sandboxrc.tax.gov.ir/req/api/")]
    [InlineData("https://sandboxrc.tax.gov.ir/req/")]
    public async Task Rejects_non_sandbox_endpoints_without_network(string url)
    {
        var handler = new FakeHandler(_ => throw new Exception("network call"));
        var transport = new LegacyInvoiceSubmissionHttpTransport(
            Config(url), () => new HttpClient(handler, disposeHandler: false));

        var result = await transport.PostAsync(
            "A2HPH4", "{\"signature\":\"sig\",\"packets\":[{}]}", "sig", "token",
            Guid.NewGuid().ToString("D"), "123", default);

        Assert.False(result.Acknowledged);
        Assert.Equal("sandbox_legacy_transport_disabled", result.SafeCode);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Missing_correlated_reference_is_indeterminate()
    {
        var handler = new FakeHandler(_ => Json(
            """{"timestamp":1,"result":[],"errors":[]}"""));
        var transport = new LegacyInvoiceSubmissionHttpTransport(
            Config(), () => new HttpClient(handler, disposeHandler: false));

        var result = await transport.PostAsync(
            "A2HPH4", ValidRequest(new string('A', 344)), new string('A', 344),
            "token", Guid.NewGuid().ToString("D"), "123", default);

        Assert.False(result.Acknowledged);
        Assert.Equal("sandbox_legacy_outcome_indeterminate", result.SafeCode);
    }
}
