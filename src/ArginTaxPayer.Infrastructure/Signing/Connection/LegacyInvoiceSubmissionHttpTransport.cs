using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

/// <summary>
/// Certificate-free legacy SELF_TSP invoice transport. This is deliberately a
/// low-level adapter: packet construction and protected-key custody stay
/// outside the HTTP boundary. Only the explicitly approved Sandbox host is
/// reachable; the production legacy host is not accepted here.
/// </summary>
internal sealed class LegacyInvoiceSubmissionHttpTransport(
    IConfiguration configuration, Func<HttpClient>? testClientFactory = null)
{
    internal sealed record Result(bool Acknowledged, string SafeCode,
        string? ReferenceNumber = null);

    private const string SandboxHost = "sandboxrc.tax.gov.ir";
    private const string SandboxPath = "/req/api/";
    private static readonly Uri InvoiceEndpoint = new(
        "https://sandboxrc.tax.gov.ir/req/api/self-tsp/async/normal-enqueue");

    internal static bool TryResolveSandboxBase(IConfiguration config, out Uri? endpoint)
    {
        endpoint = null;
        if (config["SandboxInvoiceLegacy:Enabled"] != "true" ||
            config["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] != "true")
            return false;
        if (!Uri.TryCreate(config["SandboxInvoiceLegacy:ApiBaseUrl"],
                UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
            !uri.Host.Equals(SandboxHost, StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.Equals(SandboxPath, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            return false;
        endpoint = uri;
        return true;
    }

    internal async Task<Result> PostAsync(
        string fiscalId, string requestJson, string transferSignature,
        string accessToken, string requestTraceId, string timestamp,
        CancellationToken ct)
    {
        if (!TryResolveSandboxBase(configuration, out _))
            return new(false, "sandbox_legacy_transport_disabled");
        if (!UriEquals(InvoiceEndpoint, SandboxHost, SandboxPath + "self-tsp/async/normal-enqueue") ||
            fiscalId is not { Length: >= 6 and <= 32 } ||
            fiscalId.Any(c => !char.IsAsciiLetterOrDigit(c)) ||
            requestJson is not { Length: > 2 and <= 3_000_000 } ||
            transferSignature is not { Length: > 20 and <= 4096 } ||
            accessToken is not { Length: > 0 and <= 16_384 } ||
            accessToken.Any(char.IsWhiteSpace) ||
            !Guid.TryParse(requestTraceId, out var trace) || trace == Guid.Empty ||
            timestamp is not { Length: > 0 and <= 32 } ||
            timestamp.Any(c => !char.IsAsciiDigit(c)))
            return new(false, "sandbox_legacy_packet_invalid");

        string expectedUid;
        try
        {
            using var parsedRequest = JsonDocument.Parse(requestJson);
            var root = parsedRequest.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryProperty(root, "signature", out var signature) ||
                signature.ValueKind != JsonValueKind.String ||
                !string.Equals(signature.GetString(), transferSignature, StringComparison.Ordinal) ||
                !TryProperty(root, "packets", out var packets) ||
                packets.ValueKind != JsonValueKind.Array || packets.GetArrayLength() != 1 ||
                !TryProperty(packets[0], "uid", out var packetUid) ||
                packetUid.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(packetUid.GetString(), out var uidGuid) || uidGuid == Guid.Empty)
                return new(false, "sandbox_legacy_packet_invalid");
            expectedUid = uidGuid.ToString("D");
        }
        catch (JsonException)
        {
            return new(false, "sandbox_legacy_packet_invalid");
        }

        using var client = testClientFactory?.Invoke() ?? NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, InvoiceEndpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("requestTraceId", requestTraceId);
        request.Headers.TryAddWithoutValidation("timestamp", timestamp);

        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(false, "sandbox_legacy_rejected");
            if (!response.IsSuccessStatusCode)
            {
                var classification = response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => "sandbox_legacy_http_400",
                    HttpStatusCode.NotFound => "sandbox_legacy_http_404",
                    HttpStatusCode.MethodNotAllowed => "sandbox_legacy_http_405",
                    HttpStatusCode.Conflict => "sandbox_legacy_http_409",
                    HttpStatusCode.TooManyRequests => "sandbox_legacy_http_429",
                    HttpStatusCode.BadGateway => "sandbox_legacy_http_502",
                    HttpStatusCode.ServiceUnavailable => "sandbox_legacy_http_503",
                    HttpStatusCode.GatewayTimeout => "sandbox_legacy_http_504",
                    _ => "sandbox_legacy_http_" + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture)
                };
                return new(false, classification);
            }
            using var body = await ParseBoundedAsync(response, ct);
            return ParseAcknowledgement(body, expectedUid);
        }
        catch (HttpRequestException)
        {
            return new(false, "sandbox_legacy_outcome_indeterminate");
        }
        catch (JsonException)
        {
            return new(false, "sandbox_legacy_outcome_indeterminate");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, "sandbox_legacy_outcome_indeterminate");
        }
    }

    private static Result ParseAcknowledgement(JsonDocument document, string expectedUid)
    {
        var root = document.RootElement;
        if (TryProperty(root, "errors", out var errors) &&
            errors.ValueKind != JsonValueKind.Null &&
            (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() != 0))
            return new(false, "sandbox_legacy_response_errors");
        if (!TryProperty(root, "result", out var result) ||
            result.ValueKind != JsonValueKind.Array || result.GetArrayLength() != 1)
            return new(false, "sandbox_legacy_outcome_indeterminate");

        var row = result[0];
        if (row.ValueKind != JsonValueKind.Object ||
            !TryProperty(row, "uid", out var responseUid) ||
            responseUid.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(responseUid.GetString(), out var parsedUid) ||
            parsedUid.ToString("D") != expectedUid)
            return new(false, "sandbox_legacy_response_uid_mismatch");
        if (TryProperty(row, "errorCode", out var packetError) &&
            packetError.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(packetError.GetString()))
            return new(false, "sandbox_legacy_packet_rejected");
        if (!TryProperty(row, "referenceNumber", out var reference) ||
            reference.ValueKind != JsonValueKind.String)
            return new(false, "sandbox_legacy_reference_missing");
        var value = reference.GetString();
        if (value is not { Length: > 0 and <= 128 } ||
            value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            return new(false, "sandbox_legacy_outcome_indeterminate");
        return new(true, "sandbox_legacy_reference_acknowledged", value);
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

    private static bool UriEquals(Uri uri, string host, string path) =>
        uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 &&
        uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.Equals(path, StringComparison.Ordinal);

    private static HttpClient NewClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
        MaxResponseContentBufferSize = 262_144
    };

    private static async Task<JsonDocument> ParseBoundedAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is > 262_144)
            throw new JsonException("Oversize provider response");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        while (true)
        {
            var count = await input.ReadAsync(bytes.AsMemory(), ct);
            if (count == 0) break;
            if (buffer.Length + count > 262_144)
                throw new JsonException("Oversize provider response");
            buffer.Write(bytes, 0, count);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer,
            new JsonDocumentOptions { MaxDepth = 16 }, ct);
    }
}
