using System.Net;
using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18ScopedLegacyTokenTransportTests
{
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> onRequest)
        : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Calls++;
            LastUri=req.RequestUri;
            return Task.FromResult(onRequest(req));
        }
    }

    [Theory]
    [InlineData("https://sandboxrc.tax.gov.ir/req/api/self-tsp/sync/GET_TOKEN")]
    [InlineData("https://tp.tax.gov.ir/req/api/self-tsp/sync/GET_TOKEN")]
    public async Task Scoped_token_uses_only_pinned_origin_without_caching_across_endpoint(string endpoint)
    {
        using var rsa=RSA.Create(2048);
        using var key=SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var handler=new Handler(_=>new HttpResponseMessage(HttpStatusCode.OK) {
            Content=new StringContent(
                """{"errors":[],"result":{"data":{"token":"scoped-token"}}}""",
                Encoding.UTF8,"application/json")
        });
        var config=new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?>()).Build();
        var client=new LegacyInvoiceAccessTokenClient(config,
            ()=>new HttpClient(handler,disposeHandler:false));
        var result=await client.RequestVerifiedEndpointAsync(new Uri(endpoint),
            "ABC123",key,rsa.ExportSubjectPublicKeyInfo(),default);
        Assert.True(result.Authenticated);
        Assert.Equal("scoped-token",result.AccessToken);
        Assert.Equal(endpoint,handler.LastUri!.AbsoluteUri);
        Assert.Equal(1,handler.Calls);
    }

    [Theory]
    [InlineData("https://evil.invalid/req/api/self-tsp/sync/GET_TOKEN")]
    [InlineData("http://tp.tax.gov.ir/req/api/self-tsp/sync/GET_TOKEN")]
    [InlineData("https://tp.tax.gov.ir:444/req/api/self-tsp/sync/GET_TOKEN")]
    [InlineData("https://tp.tax.gov.ir/req/api/self-tsp/async/normal-enqueue")]
    [InlineData("https://tp.tax.gov.ir/req/api/self-tsp/sync/GET_TOKEN?q=x")]
    public async Task Rejects_unverified_endpoint_without_key_use_or_network(string endpoint)
    {
        var handler=new Handler(_=>throw new Exception("network forbidden"));
        var config=new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?>()).Build();
        var client=new LegacyInvoiceAccessTokenClient(config,
            ()=>new HttpClient(handler,disposeHandler:false));
        using var rsa=RSA.Create(2048);
        using var key=SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var result=await client.RequestVerifiedEndpointAsync(new Uri(endpoint),
            "ABC123",key,rsa.ExportSubjectPublicKeyInfo(),default);
        Assert.False(result.Authenticated);
        Assert.Equal("legacy_token_endpoint_not_authorized",result.SafeCode);
        Assert.Equal(0,handler.Calls);
    }
}
