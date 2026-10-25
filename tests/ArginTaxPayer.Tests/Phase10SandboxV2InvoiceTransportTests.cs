using System.Net;
using System.Net.Http;
using System.Text;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace ArginTaxPayer.Tests;

public sealed class Phase10SandboxV2InvoiceTransportTests
{
    private static IConfiguration Config(string url="https://sandboxrc.tax.gov.ir/req/") =>
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?>{
                ["SandboxInvoiceV2:Enabled"]="true",
                ["SandboxInvoiceV2:OperatorApprovedEndpoint"]="true",
                ["SandboxInvoiceV2:ApiBaseUrl"]=url
            }).Build();

    private sealed class FakeHandler(
        Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        public List<(HttpMethod Method,string Url)> Calls {get;}=[];
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,CancellationToken ct)
        {
            Calls.Add((request.Method,request.RequestUri!.ToString()));
            return Task.FromResult(respond(request));
        }
    }
    private static string FixtureJwe()
    {
        static string Url(byte[] data)=>Convert.ToBase64String(data)
            .TrimEnd('=').Replace('+','-').Replace('/','_');
        var header=Url(Encoding.UTF8.GetBytes(
            "{\"alg\":\"RSA-OAEP-256\",\"enc\":\"A256GCM\",\"kid\":\"fixture\"}"));
        return string.Join(".",header,Url(new byte[256]),
            Url(new byte[12]),Url(new byte[32]),Url(new byte[16]));
    }

    private static string FixtureRs256Token()
    {
        static string Url(byte[] data)=>Convert.ToBase64String(data)
            .TrimEnd('=').Replace('+','-').Replace('/','_');
        return string.Join(".",Url(Encoding.UTF8.GetBytes(
            "{\"alg\":\"RS256\",\"typ\":\"JWT\"}")),
            Url(Encoding.UTF8.GetBytes("{\"nonce\":\"test\"}")),
            Url(new byte[256]));
    }

    private static HttpResponseMessage Json(string body,HttpStatusCode status=HttpStatusCode.OK)=>
        new(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};

    [Fact]
    public async Task Nonce_and_invoice_submission_are_pinned_to_Sandbox_and_correlated()
    {
        var uid=Guid.NewGuid().ToString("D");
        var handler=new FakeHandler(req=>req.RequestUri!.AbsolutePath.EndsWith("/nonce")
            ? Json("{\"nonce\":\"sandbox-test-nonce-123\"}")
            : Json("{\"result\":[{\"uid\":\""+uid+
                "\",\"referenceNumber\":\"REF-123\"}]}"));
        var client=new SandboxV2InvoiceHttpTransport(
            Config(),()=>new HttpClient(handler,disposeHandler:false));
        Assert.Equal("sandbox-test-nonce-123",await client.FetchNonceAsync(default));
        var jwe=FixtureJwe();
        var result=await client.PostPersistedPacketOnceAsync(
            jwe,"ABC123",uid,FixtureRs256Token(),default);
        Assert.True(result.Acknowledged);
        Assert.Equal("REF-123",result.ReferenceNumber);
        Assert.Equal(2,handler.Calls.Count);
        Assert.All(handler.Calls,c=>Assert.StartsWith("https://sandboxrc.tax.gov.ir/req/api/v2/",c.Url));
        Assert.Equal(HttpMethod.Get,handler.Calls[0].Method);
        Assert.Equal(HttpMethod.Post,handler.Calls[1].Method);
    }

    [Theory]
    [InlineData("https://tp.tax.gov.ir/req/")]
    [InlineData("http://sandboxrc.tax.gov.ir/req/")]
    [InlineData("https://sandboxrc.tax.gov.ir:444/req/")]
    [InlineData("https://sandboxrc.tax.gov.ir/redirect/")]
    public async Task Other_endpoints_are_rejected_without_any_network_io(string url)
    {
        var handler=new FakeHandler(_=>throw new Exception("Network should be unreachable"));
        var client=new SandboxV2InvoiceHttpTransport(
            Config(url),()=>new HttpClient(handler,disposeHandler:false));
        Assert.Null(await client.FetchNonceAsync(default));
        var result=await client.PostPersistedPacketOnceAsync(
            FixtureJwe(),
            "ABC123",Guid.NewGuid().ToString("D"),new string('z',220),default);
        Assert.False(result.Acknowledged);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Forged_dot_separated_payload_never_reaches_network()
    {
        var handler=new FakeHandler(_=>throw new Exception("Unexpected network call"));
        var transport=new SandboxV2InvoiceHttpTransport(
            Config(),()=>new HttpClient(handler,disposeHandler:false));
        var result=await transport.PostPersistedPacketOnceAsync(
            string.Join(".",Enumerable.Repeat(new string('A',45),5)),
            "ABC123",Guid.NewGuid().ToString("D"),new string('z',220),default);
        Assert.False(result.Acknowledged);
        Assert.Equal("sandbox_v2_packet_invalid",result.SafeCode);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Missing_correlated_reference_cannot_be_marked_as_accepted()
    {
        var handler=new FakeHandler(_=>Json("{\"result\":[]}"));
        var client=new SandboxV2InvoiceHttpTransport(
            Config(),()=>new HttpClient(handler,disposeHandler:false));
        var result=await client.PostPersistedPacketOnceAsync(
            string.Join(".",Enumerable.Repeat(new string('A',45),5)),
            "ABC123",Guid.NewGuid().ToString("D"),new string('z',220),default);
        Assert.False(result.Acknowledged);
        Assert.Equal("sandbox_v2_outcome_indeterminate",result.SafeCode);
    }
}
