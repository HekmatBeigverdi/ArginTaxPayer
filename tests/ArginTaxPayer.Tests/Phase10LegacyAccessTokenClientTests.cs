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
        Assert.Contains("\"packet\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"packetType\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"username\"", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PacketType\"", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain(privateKeyText, requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reuses_token_for_same_identity_only_within_the_client_scope()
    {
        using var rsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        var config = TokenConfiguration();
        var requests = 0;
        HttpClient NewClient() => new(new StubHandler(_ => TokenResponse(++requests)));
        var provider = new LegacyInvoiceAccessTokenClient(config, NewClient);

        var first = await provider.RequestAsync("ABC123", key, publicKey, CancellationToken.None);
        var repeated = await provider.RequestAsync("ABC123", key, publicKey, CancellationToken.None);
        var otherMemory = await provider.RequestAsync("DEF456", key, publicKey, CancellationToken.None);
        using var otherRsa = RSA.Create(2048);
        using var otherKey = SensitiveBytes.TakeOwnership(otherRsa.ExportPkcs8PrivateKey());
        var rotated = await provider.RequestAsync("ABC123", otherKey,
            otherRsa.ExportSubjectPublicKeyInfo(), CancellationToken.None);
        var otherScope = await new LegacyInvoiceAccessTokenClient(config, NewClient)
            .RequestAsync("ABC123", key, publicKey, CancellationToken.None);

        Assert.Equal("token-1", first.AccessToken);
        Assert.Equal("token-1", repeated.AccessToken);
        Assert.Equal("token-2", otherMemory.AccessToken);
        Assert.Equal("token-3", rotated.AccessToken);
        Assert.Equal("token-4", otherScope.AccessToken);
        Assert.Equal(4, requests);
    }

    [Fact]
    public async Task Does_not_reuse_failed_authentication_or_bypass_disabled_transport()
    {
        using var rsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var config = TokenConfiguration();
        var requests = 0;
        var provider = new LegacyInvoiceAccessTokenClient(config, () => new HttpClient(
            new StubHandler(_ => ++requests == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : TokenResponse(requests))));
        Assert.False((await provider.RequestAsync("ABC123", key,
            rsa.ExportSubjectPublicKeyInfo(), CancellationToken.None)).Authenticated);
        Assert.Equal("token-2", (await provider.RequestAsync("ABC123", key,
            rsa.ExportSubjectPublicKeyInfo(), CancellationToken.None)).AccessToken);
        config["SandboxInvoiceLegacy:Enabled"] = "false";
        Assert.False((await provider.RequestAsync("ABC123", key,
            rsa.ExportSubjectPublicKeyInfo(), CancellationToken.None)).Authenticated);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task Refreshes_short_lived_cache_and_honors_cancellation()
    {
        using var rsa = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var time = new TokenTestTimeProvider();
        var requests = 0;
        var provider = new LegacyInvoiceAccessTokenClient(TokenConfiguration(), () =>
            new HttpClient(new StubHandler(_ => TokenResponse(++requests))), time);
        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        Assert.Equal("token-1", (await provider.RequestAsync("ABC123", key,
            publicKey, CancellationToken.None)).AccessToken);
        time.Now = time.Now.AddSeconds(29);
        Assert.Equal("token-1", (await provider.RequestAsync("ABC123", key,
            publicKey, CancellationToken.None)).AccessToken);
        time.Now = time.Now.AddSeconds(1);
        Assert.Equal("token-2", (await provider.RequestAsync("ABC123", key,
            publicKey, CancellationToken.None)).AccessToken);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.RequestAsync(
            "ABC123", key, publicKey, canceled.Token));
        using var wrongRsa = RSA.Create(2048);
        using var wrongKey = SensitiveBytes.TakeOwnership(wrongRsa.ExportPkcs8PrivateKey());
        await Assert.ThrowsAsync<KeyProtectionException>(() => provider.RequestAsync(
            "ABC123", wrongKey, publicKey, CancellationToken.None));
        Assert.Equal(2, requests);
    }

    private sealed class TokenTestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static IConfigurationRoot TokenConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SandboxInvoiceLegacy:Enabled"] = "true",
            ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "true",
            ["SandboxInvoiceLegacy:ApiBaseUrl"] = "https://sandboxrc.tax.gov.ir/req/api/"
        }).Build();

    private static HttpResponseMessage TokenResponse(int number) => new(HttpStatusCode.OK)
    {
        Content = new StringContent("{\"errors\":[],\"result\":{\"data\":{\"token\":\"token-" +
            number + "\"}}}", Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
