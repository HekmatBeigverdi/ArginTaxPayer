using System.Net;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningVaultAdapterTests
{
    internal sealed class FixtureToken : IVaultWorkloadTokenSource
    {
        public ValueTask<string> ReadAsync(CancellationToken ct) => ValueTask.FromResult("synthetic-workload-token");
    }

    internal sealed class Handler : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = new();
        public Func<HttpRequestMessage, string, HttpResponseMessage>? Reply { get; set; }
        public string Version { get; set; } = VaultProtectionOptions.ReviewedServerVersion;
        public bool Root { get; set; }
        public bool Sealed { get; set; }
        public bool Exportable { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((path, body));
            if (path == "/v1/sys/health")
                return Json(new { initialized = true, @sealed = Sealed, version = Version });
            if (path == "/v1/auth/token/lookup-self")
                return Json(new { data = new { policies = Root ? new[] { "root" } : new[] { "argin-signing" }, ttl = 300 } });
            if (path == "/v1/transit/keys/signing-kek")
                return Json(new { data = new { type = "aes256-gcm96", derived = true, convergent_encryption = false,
                    exportable = Exportable, allow_plaintext_backup = false, deletion_allowed = false } });
            Assert.Equal("synthetic-workload-token", request.Headers.GetValues("X-Vault-Token").Single());
            return Reply?.Invoke(request, body) ?? Json(new { data = new { } });
        }
        public static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    }

    internal static VaultProtectionOptions Options(string address = "https://vault.example.invalid/") =>
        new(new Uri(address), "transit", "signing-kek", "signing", "/private/agent-token",
            VaultProtectionOptions.ReviewedServerVersion);

    internal static VaultHttpTransport Transport(Handler handler) => new(Options(), new FixtureToken(), handler);

    [Theory]
    [InlineData("http://vault.example.invalid/")]
    [InlineData("https://user:password@vault.example.invalid/")]
    [InlineData("https://vault.example.invalid/prefix")]
    [InlineData("https://vault.example.invalid/?other=1")]
    public void Unsafe_endpoint_is_rejected(string address) =>
        Assert.Throws<KeyProtectionException>(() => Options(address).Validate());

    [Fact]
    public async Task Sealed_or_wrong_version_or_root_vault_is_denied_before_secret_request()
    {
        foreach (var kind in new[] { "sealed", "version", "root", "exportable" })
        {
            var handler = new Handler
            {
                Sealed = kind == "sealed", Root = kind == "root",
                Exportable = kind == "exportable",
                Version = kind == "version" ? "0.0.0" : VaultProtectionOptions.ReviewedServerVersion
            };
            using var transport = Transport(handler);
            var provider = new VaultKeyProtectionProvider(transport, Options());
            await Assert.ThrowsAsync<KeyProtectionException>(() =>
                provider.WrapAsync(Phase09SigningKeyProtectionTests.Identity(), new byte[32], default));
            Assert.DoesNotContain(handler.Requests, r => r.Path.Contains("/encrypt/"));
        }
    }

    [Fact]
    public async Task Transit_wrap_and_unwrap_use_bound_aad_and_exact_32_byte_dek()
    {
        var id = Phase09SigningKeyProtectionTests.Identity();
        var dek = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var handler = new Handler();
        handler.Reply = (request, text) =>
        {
            using var json = JsonDocument.Parse(text);
            Assert.Equal(Convert.ToBase64String(id.TransitBinding()),
                json.RootElement.GetProperty("context").GetString());
            if (request.RequestUri!.AbsolutePath.Contains("/encrypt/"))
            {
                Assert.Equal(Convert.ToBase64String(dek), json.RootElement.GetProperty("plaintext").GetString());
                return Handler.Json(new { data = new { ciphertext = "vault:v1:ZmFrZQ==" } });
            }
            return Handler.Json(new { data = new { plaintext = Convert.ToBase64String(dek) } });
        };
        using var transport = Transport(handler);
        var provider = new VaultKeyProtectionProvider(transport, Options());
        var wrapped = await provider.WrapAsync(id, dek, default);
        Assert.Equal("vault:v1", wrapped.ProtectionKeyVersion);
        using var clear = await provider.UnwrapAsync(id, wrapped, default);
        Assert.Equal(dek, clear.Bytes.ToArray());
    }

    [Fact]
    public async Task Unexpected_dek_size_and_inconsistent_wrapping_version_are_denied()
    {
        var handler = new Handler { Reply = (_, _) =>
            Handler.Json(new { data = new { plaintext = Convert.ToBase64String(new byte[31]) } }) };
        using var transport = Transport(handler);
        var provider = new VaultKeyProtectionProvider(transport, Options());
        var id = Phase09SigningKeyProtectionTests.Identity();
        await Assert.ThrowsAsync<KeyProtectionException>(() =>
            provider.UnwrapAsync(id, new("vault:v1:ZmFrZQ==", "vault:v1"), default));
        await Assert.ThrowsAsync<KeyProtectionException>(() =>
            provider.UnwrapAsync(id, new("vault:v1:ZmFrZQ==", "vault:v2"), default));
    }

    [Theory]
    [InlineData(302)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task Provider_errors_are_redacted_and_never_automatically_retried(int code)
    {
        var handler = new Handler { Reply = (_, _) => new((HttpStatusCode)code)
        { Content = new StringContent("secret-canary") } };
        using var transport = Transport(handler);
        var provider = new VaultKeyProtectionProvider(transport, Options());
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() =>
            provider.WrapAsync(Phase09SigningKeyProtectionTests.Identity(), new byte[32], default));
        Assert.DoesNotContain("secret-canary", error.ToString());
        Assert.Null(error.InnerException);
        Assert.Single(handler.Requests.Where(r => r.Path.Contains("/encrypt/")));
    }

    [Fact]
    public async Task Kv_create_is_cas_zero_and_unknown_write_is_recovery_required()
    {
        var id = Phase09SigningKeyProtectionTests.Identity();
        using var synthetic = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var envelope = await new KeyEnvelopeProtector(synthetic).ProtectAsync(id, new byte[] { 1 });
        var handler = new Handler();
        handler.Reply = (_, text) =>
        {
            using var json = JsonDocument.Parse(text);
            Assert.Equal(0, json.RootElement.GetProperty("options").GetProperty("cas").GetInt32());
            Assert.False(json.RootElement.GetProperty("data").TryGetProperty("PrivateKey", out JsonElement _));
            return Handler.Json(new { data = new { version = 1 } });
        };
        using var transport = Transport(handler);
        var store = new VaultProtectedKeyEnvelopeStore(transport, Options());
        Assert.Equal(1, (await store.CreateAsync(id, envelope, default)).Version);
        handler.Reply = (_, _) => throw new HttpRequestException("secret-canary");
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() => store.CreateAsync(id, envelope, default));
        Assert.Equal(KeyProtectionFailure.RecoveryRequired, error.Failure);
        Assert.DoesNotContain("secret-canary", error.ToString());
    }

    [Fact]
    public async Task Kv_cas_conflict_is_not_overwritten_or_deleted()
    {
        var id = Phase09SigningKeyProtectionTests.Identity();
        using var synthetic = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var envelope = await new KeyEnvelopeProtector(synthetic).ProtectAsync(id, new byte[] { 1 });
        var handler = new Handler { Reply = (_, _) => Handler.Json(new { errors = new[] { "conflict" } }, HttpStatusCode.BadRequest) };
        using var transport = Transport(handler);
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() =>
            new VaultProtectedKeyEnvelopeStore(transport, Options()).CreateAsync(id, envelope, default));
        Assert.Equal(KeyProtectionFailure.Conflict, error.Failure);
        Assert.Single(handler.Requests.Where(r => r.Path.Contains("/data/")));
    }

    private sealed class UnknownLengthStream : MemoryStream
    {
        public UnknownLengthStream() : base(new byte[65537]) { }
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("malformed")]
    [InlineData("oversized")]
    public async Task Unknown_durable_response_requires_recovery_and_never_retries(string failure)
    {
        var id = Phase09SigningKeyProtectionTests.Identity();
        using var synthetic = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var envelope = await new KeyEnvelopeProtector(synthetic).ProtectAsync(id, new byte[] { 1 });
        var handler = new Handler();
        handler.Reply = (_, _) => failure switch
        {
            "cancel" => throw new TaskCanceledException("secret-canary"),
            "malformed" => new(HttpStatusCode.OK) { Content = new StringContent("secret-canary") },
            _ => new(HttpStatusCode.OK) { Content = new StreamContent(new UnknownLengthStream()) }
        };
        using var transport = Transport(handler);
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() =>
            new VaultProtectedKeyEnvelopeStore(transport, Options()).CreateAsync(id, envelope, default));
        Assert.Equal(KeyProtectionFailure.RecoveryRequired, error.Failure);
        Assert.DoesNotContain("secret-canary", error.ToString());
        Assert.Null(error.InnerException);
        Assert.Single(handler.Requests.Where(r => r.Path.Contains("/data/")));
    }

    [Fact]
    public async Task Kv_read_rejects_envelope_identity_swap_and_accepts_only_missing_as_absence()
    {
        var id = Phase09SigningKeyProtectionTests.Identity();
        using var synthetic = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var envelope = await new KeyEnvelopeProtector(synthetic).ProtectAsync(id, new byte[] { 1 });
        var handler = new Handler { Reply = (_, _) =>
            Handler.Json(new { data = new { data = envelope, metadata = new { version = 1 } } }) };
        using var transport = Transport(handler);
        var store = new VaultProtectedKeyEnvelopeStore(transport, Options());
        Assert.Equal(id, (await store.FindAsync(id, default))!.Envelope.Identity);
        await Assert.ThrowsAsync<KeyProtectionException>(() =>
            store.FindAsync(id with { CustomerUserId = Guid.NewGuid() }, default));
        handler.Reply = (_, _) => new(HttpStatusCode.NotFound);
        Assert.Null(await store.FindAsync(id, default));
        handler.Reply = (_, _) => new(HttpStatusCode.Forbidden);
        await Assert.ThrowsAsync<KeyProtectionException>(() => store.FindAsync(id, default));
    }
}
