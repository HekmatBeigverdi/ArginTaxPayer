using System.Net;
using System.Text;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18ScopedLegacyHttpTransportTests
{
    private const string RequestUid = "a3c88da5-ecbd-46b1-9bc3-b47df196a873";
    private static string Packet(string signature) =>
        "{\"signature\":\"" + signature + "\",\"packets\":[{\"uid\":\"" + RequestUid + "\"}]}";

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Sent { get; private set; }
        public Uri? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Sent++;
            LastUri = request.RequestUri;
            return Task.FromResult(reply(request));
        }
    }

    private static LegacyInvoiceSubmissionHttpTransport Transport(Handler handler) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?>()).Build(),
            () => new HttpClient(handler, disposeHandler:false));

    [Theory]
    [InlineData("https://sandboxrc.tax.gov.ir/req/api/self-tsp/async/normal-enqueue")]
    [InlineData("https://tp.tax.gov.ir/req/api/self-tsp/async/normal-enqueue")]
    public async Task Same_validated_wire_and_correlated_receipt_work_with_both_pinned_origins(string origin)
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                "{\"result\":[{\"uid\":\"" + RequestUid +
                "\",\"referenceNumber\":\"reference-14\",\"errorCode\":null}],\"errors\":[]}",
                Encoding.UTF8, "application/json")
        });
        var transport = Transport(handler);
        var signature = new string('A',344);
        var result = await transport.PostVerifiedEndpointAsync(new Uri(origin),
            "A2HPH4",Packet(signature), signature,"token",
            Guid.NewGuid().ToString("D"),"12345",default);
        Assert.True(result.Acknowledged);
        Assert.Equal("reference-14",result.ReferenceNumber);
        Assert.Equal(1,handler.Sent);
        Assert.Equal(origin,handler.LastUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://other.example/req/api/self-tsp/async/normal-enqueue")]
    [InlineData("http://tp.tax.gov.ir/req/api/self-tsp/async/normal-enqueue")]
    [InlineData("https://tp.tax.gov.ir:444/req/api/self-tsp/async/normal-enqueue")]
    [InlineData("https://tp.tax.gov.ir/req/api/self-tsp/sync/GET_TOKEN")]
    [InlineData("https://tp.tax.gov.ir/req/api/self-tsp/async/normal-enqueue?q=1")]
    [InlineData("https://user:password@tp.tax.gov.ir/req/api/self-tsp/async/normal-enqueue")]
    public async Task Refuses_unpinned_or_modified_production_endpoint_without_http(string origin)
    {
        var handler = new Handler(_=>throw new Exception("unexpected network call"));
        var result = await Transport(handler).PostVerifiedEndpointAsync(new Uri(origin),
            "A2HPH4",Packet(new string('A',344)),new string('A',344),
            "token",Guid.NewGuid().ToString("D"),"12345",default);
        Assert.False(result.Acknowledged);
        Assert.Equal(0,handler.Sent);
    }

    [Fact]
    public async Task Mismatched_response_uid_is_not_acknowledged_in_production()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                """{"result":[{"uid":"dbd7c0b2-b8bf-4e8e-a41d-21648f07716d","referenceNumber":"ref-999"}],"errors":[]}""",
                Encoding.UTF8,"application/json")
        });
        var signature = new string('B',344);
        var result = await Transport(handler).PostVerifiedEndpointAsync(
            new Uri("https://tp.tax.gov.ir/req/api/self-tsp/async/normal-enqueue"),
            "A2HPH4",Packet(signature),signature,"token",
            Guid.NewGuid().ToString("D"),"12345",default);
        Assert.False(result.Acknowledged);
        Assert.Null(result.ReferenceNumber);
        Assert.Equal(1,handler.Sent);
    }
}
