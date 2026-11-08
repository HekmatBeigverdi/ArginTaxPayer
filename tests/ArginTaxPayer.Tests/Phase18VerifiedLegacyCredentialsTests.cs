using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18VerifiedLegacyCredentialsTests
{
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(responder(message));
        }
    }

    [Theory]
    [InlineData("sandboxrc.tax.gov.ir")]
    [InlineData("tp.tax.gov.ir")]
    public async Task Verified_routes_recover_token_and_server_key_from_same_origin(string host)
    {
        using var signer=RSA.Create(2048);
        using var signerKey=SensitiveBytes.TakeOwnership(signer.ExportPkcs8PrivateKey());
        using var authority=RSA.Create(2048);
        var tokenRequests=new Stub(req => {
            Assert.Equal(host,req.RequestUri!.Host);
            Assert.EndsWith("/self-tsp/sync/GET_TOKEN",req.RequestUri.AbsolutePath);
            return Json(new { errors=Array.Empty<object>(), result=new { data=new { token="authority-token" } } });
        });
        var serverRequests=new Stub(req => {
            Assert.Equal(host,req.RequestUri!.Host);
            Assert.EndsWith("/self-tsp/sync/GET_SERVER_INFORMATION",req.RequestUri.AbsolutePath);
            return Json(new { errors=Array.Empty<object>(), result=new { data=new {
                publicKeys=new[] { new { id="trusted-key",key=Convert.ToBase64String(authority.ExportSubjectPublicKeyInfo()) } }
            } } });
        });
        var config=new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?>()).Build();
        var tokens=new LegacyInvoiceAccessTokenClient(config,
            ()=>new HttpClient(tokenRequests,disposeHandler:false));
        var server=new LegacySandboxServerEncryptionKeyClient(config,
            ()=>new HttpClient(serverRequests,disposeHandler:false));
        using var credentials=await LegacyInvoicePreparationCredentials.RequestVerifiedAsync(
            tokens,server,
            new Uri($"https://{host}/req/api/self-tsp/sync/GET_TOKEN"),
            new Uri($"https://{host}/req/api/self-tsp/sync/GET_SERVER_INFORMATION"),
            "ABC123",signerKey,signer.ExportSubjectPublicKeyInfo(),default);
        Assert.True(credentials.Token.Authenticated);
        Assert.Equal("authority-token",credentials.Token.AccessToken);
        Assert.Equal("trusted-key",credentials.EncryptionKey?.KeyId);
        Assert.Equal(1,tokenRequests.Calls);
        Assert.Equal(1,serverRequests.Calls);
    }

    [Fact]
    public async Task Refuses_wrong_server_information_host_without_requesting_it()
    {
        using var signer=RSA.Create(2048);
        using var key=SensitiveBytes.TakeOwnership(signer.ExportPkcs8PrivateKey());
        var config=new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?>()).Build();
        var tokenRequests=new Stub(_=>new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var serverRequests=new Stub(_=>throw new Exception("network forbidden"));
        var tokens=new LegacyInvoiceAccessTokenClient(config,
            ()=>new HttpClient(tokenRequests,disposeHandler:false));
        var server=new LegacySandboxServerEncryptionKeyClient(config,
            ()=>new HttpClient(serverRequests,disposeHandler:false));
        using var result=await LegacyInvoicePreparationCredentials.RequestVerifiedAsync(
            tokens,server,
            new Uri("https://tp.tax.gov.ir/req/api/self-tsp/sync/GET_TOKEN"),
            new Uri("https://elsewhere.invalid/req/api/self-tsp/sync/GET_SERVER_INFORMATION"),
            "ABC123",key,signer.ExportSubjectPublicKeyInfo(),default);
        Assert.False(result.Token.Authenticated);
        Assert.Null(result.EncryptionKey);
        Assert.Equal(0,serverRequests.Calls);
    }

    private static HttpResponseMessage Json(object value) =>
        new(HttpStatusCode.OK) {
            Content=new StringContent(JsonSerializer.Serialize(value),Encoding.UTF8,"application/json")
        };
}
