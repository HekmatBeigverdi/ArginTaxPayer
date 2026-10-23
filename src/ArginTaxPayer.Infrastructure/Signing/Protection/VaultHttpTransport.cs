using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

// Deliberately bypasses HttpClientFactory logging. TLS uses the OS trust store; no custom trust bypass.
// No redirects, cookies, automatic retries or response/error/body logging.
internal sealed class VaultHttpTransport : IDisposable
{
    private readonly HttpClient client;
    private readonly IVaultWorkloadTokenSource tokens;
    private readonly VaultProtectionOptions options;
    private const int MaxResponseBytes = 64 * 1024;

    public VaultHttpTransport(VaultProtectionOptions options, IVaultWorkloadTokenSource tokens)
        : this(options, tokens, new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        }) { }

    // Internal and accessible only to the friend test assembly; not a configurable TLS/handler hook.
    internal VaultHttpTransport(VaultProtectionOptions options, IVaultWorkloadTokenSource tokens,
        HttpMessageHandler handler)
    {
        options.Validate();
        this.options = options;
        this.tokens = tokens;
        client = new HttpClient(handler, disposeHandler: true)
        { BaseAddress = options.Address, Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<JsonDocument?> SendAsync(HttpMethod method, string path, object? body,
        bool allowMissing, bool durableWrite, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        ct = deadline.Token;
        try
        {
            // Admission is repeated, not cached across an Agent token rotation or Vault re-seal.
            using var health = await ExchangeAsync(HttpMethod.Get, "v1/sys/health", null, null, false, false, ct);
            if (health is null || !health.RootElement.GetProperty("initialized").GetBoolean() ||
                health.RootElement.GetProperty("sealed").GetBoolean() ||
                health.RootElement.GetProperty("version").GetString() != options.ServerVersion)
                throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
            var token = await tokens.ReadAsync(ct);
            using var self = await ExchangeAsync(HttpMethod.Get, "v1/auth/token/lookup-self", null, token, false, false, ct);
            var data = self!.RootElement.GetProperty("data");
            var policies = data.GetProperty("policies").EnumerateArray().Select(p => p.GetString()).ToArray();
            var identityPolicies = data.TryGetProperty("identity_policies", out var inherited)
                ? inherited.EnumerateArray().Select(p => p.GetString()).ToArray() : Array.Empty<string?>();
            if (data.GetProperty("ttl").GetInt64() < 60 || !policies.Contains("argin-signing") ||
                policies.Concat(identityPolicies).Any(p => p is not ("default" or "argin-signing")))
                throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
            using var key = await ExchangeAsync(HttpMethod.Get,
                $"v1/{options.TransitMount}/keys/{options.TransitKey}", null, token, false, false, ct);
            var keyData = key!.RootElement.GetProperty("data");
            if (keyData.GetProperty("type").GetString() != "aes256-gcm96" ||
                !keyData.GetProperty("derived").GetBoolean() ||
                keyData.GetProperty("convergent_encryption").GetBoolean() ||
                keyData.GetProperty("exportable").GetBoolean() ||
                keyData.GetProperty("allow_plaintext_backup").GetBoolean() ||
                keyData.GetProperty("deletion_allowed").GetBoolean())
                throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
            return await ExchangeAsync(method, path, body, token, allowMissing, durableWrite, ct);
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
        catch (OperationCanceledException)
        { throw new KeyProtectionException(KeyProtectionFailure.Unavailable); }
    }

    private async Task<JsonDocument?> ExchangeAsync(HttpMethod method, string path, object? payload,
        string? token, bool allowMissing, bool durableWrite, CancellationToken ct)
    {
        byte[]? requestBytes = null;
        try
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            if (token is not null) request.Headers.Add("X-Vault-Token", token);
            if (payload is not null)
            {
                requestBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
                request.Content = new ByteArrayContent(requestBytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (allowMissing && method == HttpMethod.Get && response.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
            {
                var failure = durableWrite && (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
                    ? KeyProtectionFailure.Conflict
                    : durableWrite && (int)response.StatusCode >= 500
                        ? KeyProtectionFailure.RecoveryRequired : KeyProtectionFailure.Unavailable;
                throw new KeyProtectionException(failure);
            }
            if (response.Content.Headers.ContentLength > MaxResponseBytes)
                throw new KeyProtectionException(durableWrite
                    ? KeyProtectionFailure.RecoveryRequired : KeyProtectionFailure.Integrity);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            // Clear owned raw response bytes, including Transit plaintext-DEK responses.
            var bytes = new byte[MaxResponseBytes + 1];
            try
            {
                var length = 0;
                while (length < bytes.Length)
                {
                    var read = await stream.ReadAsync(bytes.AsMemory(length), ct);
                    if (read == 0) break;
                    length += read;
                }
                if (length > MaxResponseBytes)
                    throw new KeyProtectionException(durableWrite
                        ? KeyProtectionFailure.RecoveryRequired : KeyProtectionFailure.Integrity);
                return ParseResponse(bytes, length);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException)
        {
            // A timed-out/cancelled durable write may have committed. Caller must reconcile by identity.
            throw new KeyProtectionException(durableWrite
                ? KeyProtectionFailure.RecoveryRequired : KeyProtectionFailure.Unavailable);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new KeyProtectionException(durableWrite
                ? KeyProtectionFailure.RecoveryRequired : KeyProtectionFailure.Integrity);
        }
        finally
        {
            if (requestBytes is not null) CryptographicOperations.ZeroMemory(requestBytes);
        }
    }

    private static JsonDocument ParseResponse(byte[] bytes, int length)
    {
        var reader = new Utf8JsonReader(bytes.AsSpan(0, length), new JsonReaderOptions { MaxDepth = 24 });
        var document = JsonDocument.ParseValue(ref reader);
        try
        {
            if (reader.Read()) throw new JsonException();
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    public void Dispose() => client.Dispose();
}
