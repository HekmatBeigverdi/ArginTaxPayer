using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10LegacyPreparationCredentialsTests
{
    // A sequential implementation deadlocks at the provider barrier and times out.
    [Fact]
    public async Task Independent_provider_requests_overlap_and_return_usable_credentials()
    {
        using var signer = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(signer.ExportPkcs8PrivateKey());
        using var authority = RSA.Create(2048);
        var tokenStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var encryptionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var config = Configuration();
        var tokens = new LegacyInvoiceAccessTokenClient(config, () => new HttpClient(new Handler(async ct =>
        {
            tokenStarted.SetResult();
            await encryptionStarted.Task.WaitAsync(ct);
            return Json(new { errors = Array.Empty<object>(), result = new { data = new { token = "test-token" } } });
        })));
        var encryption = new LegacySandboxServerEncryptionKeyClient(config, () => new HttpClient(new Handler(async ct =>
        {
            encryptionStarted.SetResult();
            await tokenStarted.Task.WaitAsync(ct);
            return Json(new { errors = Array.Empty<object>(), result = new { data = new {
                publicKeys = new[] { new { id = "authority-key", key = Convert.ToBase64String(authority.ExportSubjectPublicKeyInfo()) } }
            } } });
        })));

        using var result = await LegacyInvoicePreparationCredentials.RequestAsync(
            tokens, encryption, "ABC123", key, signer.ExportSubjectPublicKeyInfo(), timeout.Token);

        Assert.True(result.Token.Authenticated);
        Assert.Equal("test-token", result.Token.AccessToken);
        Assert.NotNull(result.EncryptionKey);
        Assert.Equal("authority-key", result.EncryptionKey.KeyId);
        Assert.Equal(authority.ExportSubjectPublicKeyInfo(), result.EncryptionKey.PublicSpkiDer);
        var publicBytes = result.EncryptionKey.PublicSpkiDer;
        result.Dispose();
        Assert.All(publicBytes, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public async Task Rejected_token_does_not_become_success_when_encryption_succeeds()
    {
        using var signer = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(signer.ExportPkcs8PrivateKey());
        using var authority = RSA.Create(2048);
        var config = Configuration();
        var tokens = new LegacyInvoiceAccessTokenClient(config, () => new HttpClient(new Handler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)))));
        var encryption = new LegacySandboxServerEncryptionKeyClient(config, () => new HttpClient(new Handler(_ =>
            Task.FromResult(Json(new { result = new { data = new { publicKeys = new[] {
                new { id = "authority-key", key = Convert.ToBase64String(authority.ExportSubjectPublicKeyInfo()) }
            } } } })))));

        using var result = await LegacyInvoicePreparationCredentials.RequestAsync(
            tokens, encryption, "ABC123", key, signer.ExportSubjectPublicKeyInfo(), CancellationToken.None);

        Assert.False(result.Token.Authenticated);
        Assert.Equal("sandbox_legacy_token_rejected", result.Token.SafeCode);
        Assert.Null(result.Token.AccessToken);
    }

    [Fact]
    public async Task Cancellation_waits_for_both_requests_to_release_the_signing_key()
    {
        using var signer = RSA.Create(2048);
        using var key = SensitiveBytes.TakeOwnership(signer.ExportPkcs8PrivateKey());
        using var cancellation = new CancellationTokenSource();
        var config = Configuration();
        var encryptionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var encryptionFinished = false;
        var tokens = new LegacyInvoiceAccessTokenClient(config, () => new HttpClient(new Handler(async ct =>
        {
            await encryptionStarted.Task.WaitAsync(ct);
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();
            return Json(new { });
        })));
        var encryption = new LegacySandboxServerEncryptionKeyClient(config, () => new HttpClient(new Handler(async ct =>
        {
            encryptionStarted.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            finally { encryptionFinished = true; }
            return Json(new { });
        })));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LegacyInvoicePreparationCredentials.RequestAsync(tokens, encryption,
                "ABC123", key, signer.ExportSubjectPublicKeyInfo(), cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(encryptionFinished);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> {
            ["SandboxInvoiceLegacy:Enabled"] = "true",
            ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "true",
            ["SandboxInvoiceLegacy:ApiBaseUrl"] = "https://sandboxrc.tax.gov.ir/req/api/"
        }).Build();

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) {
        Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(ct);
    }
}
