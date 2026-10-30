using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

internal sealed record LegacyInvoiceAccessTokenResult(
    bool Authenticated, string SafeCode, string? AccessToken = null);

/// <summary>
/// Obtains a legacy SELF_TSP token at Outbox-consume time. The token is only
/// held in memory and is never included in an Outbox payload or log entry.
/// </summary>
internal sealed class LegacyInvoiceAccessTokenClient(
    IConfiguration configuration, Func<HttpClient>? testClientFactory = null,
    TimeProvider? timeProvider = null)
{
    // DI registers this client as scoped: tokens never cross request/worker scopes.
    // Keep reuse short even if an authority returns an opaque token without expiry.
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<(Uri Endpoint, string FiscalId, string PublicKey),
        (LegacyInvoiceAccessTokenResult Result, DateTimeOffset ReuseUntil)> tokens = new();
    private const int MaxResponseBytes = 262_144;
    private static readonly JsonSerializerOptions LegacyJsonOptions = new(JsonSerializerDefaults.Web);

    internal async Task<LegacyInvoiceAccessTokenResult> RequestAsync(
        string fiscalId, SensitiveBytes signingKey, ReadOnlyMemory<byte> expectedPublicSpki,
        CancellationToken ct)
    {
        if (!LegacyInvoiceSubmissionHttpTransport.TryResolveSandboxBase(configuration, out var baseUri) ||
            baseUri is null)
            return new(false, "sandbox_legacy_token_disabled");
        if (fiscalId is not { Length: >= 6 and <= 32 } ||
            fiscalId.Any(c => !char.IsAsciiLetterOrDigit(c)))
            return new(false, "sandbox_legacy_token_invalid");
        ArgumentNullException.ThrowIfNull(signingKey);
        if (expectedPublicSpki.Length is < 1 or > 4096)
            return new(false, "sandbox_legacy_token_invalid");

        ct.ThrowIfCancellationRequested();
        var requestedAt = clock.GetUtcNow();
        var trace = Guid.NewGuid().ToString("D");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var packet = new
        {
            Uid = Guid.NewGuid().ToString("D"),
            PacketType = "GET_TOKEN",
            Retry = false,
            Data = new { Username = fiscalId },
            EncryptionKeyId = (string?)null,
            SymmetricKey = (string?)null,
            Iv = (string?)null,
            FiscalId = fiscalId,
            DataSignature = (string?)null
        };
        var signature = new LegacyOfflineSigningEngine().SignObject(
            signingKey, expectedPublicSpki, packet, LegacySigningPurpose.Transfer,
            new Dictionary<string, string> { ["requestTraceId"] = trace, ["timestamp"] = timestamp })
            .SignatureBase64;

        var endpoint = new Uri(baseUri, "self-tsp/sync/GET_TOKEN");
        // Sign first so reuse cannot bypass private/public key integrity checks.
        var identity = (endpoint, fiscalId, Convert.ToHexString(SHA256.HashData(expectedPublicSpki.Span)));
        if (tokens.TryGetValue(identity, out var cached) && requestedAt < cached.ReuseUntil)
            return cached.Result;
        tokens.Remove(identity);
        using var client = testClientFactory?.Invoke() ?? NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                Signature = signature,
                SignatureKeyId = (string?)null,
                Packet = new
                {
                    packet.Uid, packet.PacketType, packet.Retry, packet.Data,
                    packet.EncryptionKeyId, packet.SymmetricKey, packet.Iv,
                    packet.FiscalId, packet.DataSignature,
                    SignatureKeyId = (string?)null
                }
            }, options: LegacyJsonOptions)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("requestTraceId", trace);
        request.Headers.TryAddWithoutValidation("timestamp", timestamp);

        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(false, "sandbox_legacy_token_rejected");
            if (!response.IsSuccessStatusCode)
                return new(false, "sandbox_legacy_token_unavailable");
            using var document = await ParseBoundedAsync(response, ct);
            if (!TryReadToken(document, out var token))
                return new(false, "sandbox_legacy_token_rejected");
            var result = new LegacyInvoiceAccessTokenResult(true, "sandbox_legacy_token_received", token);
            tokens[identity] = (result, requestedAt.AddSeconds(30));
            return result;
        }
        catch (HttpRequestException)
        {
            return new(false, "sandbox_legacy_token_unavailable");
        }
        catch (JsonException)
        {
            return new(false, "sandbox_legacy_token_invalid");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, "sandbox_legacy_token_unavailable");
        }
    }

    private static bool TryReadToken(JsonDocument document, out string token)
    {
        token = string.Empty;
        var root = document.RootElement;
        if (TryProperty(root, "errors", out var errors) &&
            errors.ValueKind != JsonValueKind.Null &&
            (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() != 0))
            return false;
        if (!TryProperty(root, "result", out var result) ||
            result.ValueKind != JsonValueKind.Object ||
            !TryProperty(result, "data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !TryProperty(data, "token", out var field) ||
            field.ValueKind != JsonValueKind.String)
            return false;
        token = field.GetString() ?? string.Empty;
        return token is { Length: > 0 and <= 16_384 } &&
            token.All(c => c is >= (char)33 and <= (char)126) &&
            !token.Any(char.IsWhiteSpace);
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        return false;
    }

    private static HttpClient NewClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
        MaxResponseContentBufferSize = MaxResponseBytes
    };

    private static async Task<JsonDocument> ParseBoundedAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            throw new JsonException("Oversize provider response");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        while (true)
        {
            var count = await input.ReadAsync(bytes.AsMemory(), ct);
            if (count == 0) break;
            if (buffer.Length + count > MaxResponseBytes)
                throw new JsonException("Oversize provider response");
            buffer.Write(bytes, 0, count);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer,
            new JsonDocumentOptions { MaxDepth = 16 }, ct);
    }
}
