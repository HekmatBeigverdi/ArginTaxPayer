using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

/// <summary>
/// Scoped certificate-free SELF_TSP integration based on TaxCollectData SDK 0.0.30:
/// ObjectTransferApi / GET_TOKEN / GET_FISCAL_INFORMATION.
/// Never copies the original SDK's insecure global TLS-validation override.
/// </summary>
internal sealed class LegacyTaxAuthorityConnection(
    ArginTaxPayerDbContext db,
    EfSigningOperationRepository authorization,
    ProtectedKeyEnvelopeStore custody,
    KeyEnvelopeProtector protector,
    HttpClient http,
    ILogger<LegacyTaxAuthorityConnection> logger) : ISigningReadOnlyConnectionTest, IDisposable
{
    internal const string BaseUrl = "https://tp.tax.gov.ir/req/api/";
    public void Dispose() => http.Dispose();
    private static readonly Uri TokenUri = new(BaseUrl + "self-tsp/sync/GET_TOKEN");
    private static readonly Uri FiscalUri = new(BaseUrl + "self-tsp/sync/GET_FISCAL_INFORMATION");
    internal const string FiscalInformationResponsePacketType = "FISCAL_INFORMATION";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SigningConnectionTestResult> TestAsync(string? bearer, Guid taxpayerId,
        Guid fiscalMemoryId, CancellationToken ct = default)
    {
        SigningConnectionTestResult Outcome(SigningConnectionTestStatus status, FiscalInformationSnapshot? info = null) =>
            SigningConnectionTestResult.Create(status, DateTime.UtcNow) with { FiscalInformation = info };
        try
        {
            var scope = await authorization.ReadImportOnboardingAsync(bearer, taxpayerId, fiscalMemoryId, ct);
            if (scope.Status != SigningGenerationStatus.Replayed)
                return Outcome(SigningConnectionTestStatus.NotAuthorized);
            if (!scope.HasKey || scope.LifecycleState != 2)
                return Outcome(SigningConnectionTestStatus.KeyNotReady);

            // Authorization is checked before any query resolving the Vault key locator.
            var ownerId = await db.TaxpayerProfiles.AsNoTracking()
                .Where(x => x.Id == taxpayerId).Select(x => x.CustomerUserId).SingleOrDefaultAsync(ct);
            var fiscalId = await db.FiscalMemories.AsNoTracking()
                .Where(x => x.Id == fiscalMemoryId && x.TaxpayerId == taxpayerId)
                .Select(x => x.MemoryIdentifier).SingleOrDefaultAsync(ct);
            if (ownerId is null || ownerId == Guid.Empty || string.IsNullOrWhiteSpace(fiscalId) || fiscalId.Length > 64 ||
                fiscalId.Any(x => !char.IsAsciiLetterOrDigit(x)))
                return Outcome(SigningConnectionTestStatus.KeyNotReady);
            var owner = ownerId.Value;

            var selection = await db.SigningKeySelections.AsNoTracking()
                .Where(x => x.FiscalMemoryId == fiscalMemoryId && x.CustomerUserId == owner &&
                    x.TaxpayerId == taxpayerId)
                .Select(x => x.KeyVersionId).SingleOrDefaultAsync(ct);
            var query = db.SigningKeyVersions.AsNoTracking().Where(x =>
                x.CustomerUserId == owner && x.TaxpayerId == taxpayerId &&
                x.FiscalMemoryId == fiscalMemoryId && x.LifecycleState == 2 && x.PublicSpkiDer != null);
            var key = selection is { } selected
                ? await query.SingleOrDefaultAsync(x => x.Id == selected, ct)
                : await query.OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
            if (key is null || key.OriginOperationRecordId == Guid.Empty || key.PublicSpkiDer is null ||
                key.PublicSpkiDer.Length is < 1 or > 4096)
                return Outcome(SigningConnectionTestStatus.KeyNotReady);

            var identity = new KeyProtectionIdentity(owner, taxpayerId, fiscalMemoryId,
                key.Id, key.OriginOperationRecordId);
            var envelope = await custody.RecoverAsync(identity, ct);
            if (envelope is null) return Outcome(SigningConnectionTestStatus.VaultUnavailable);
            using var secret = await protector.UnprotectAsync(identity, envelope.Envelope, ct);

            // The same vault-bound key is used for imported AND generated credentials.
            using var tokenResponse = await SendAsync(TokenUri, "GET_TOKEN", fiscalId,
                new { Username = fiscalId }, secret, key.PublicSpkiDer, null, ct);
            if (!TryGetSdkResponseData(tokenResponse, out var tokenData) ||
                !TryProperty(tokenData, "token", out var tokenField) ||
                tokenField.ValueKind != JsonValueKind.String ||
                tokenField.GetString() is not { Length: > 0 and < 16384 } token ||
                token.Any(ch => ch < 33 || ch > 126) ||
                token.Any(char.IsWhiteSpace))
                return Outcome(SigningConnectionTestStatus.Indeterminate);

            using var fiscalResponse = await SendAsync(FiscalUri, "GET_FISCAL_INFORMATION", fiscalId,
                fiscalId, secret, key.PublicSpkiDer, token, ct);
            if (!TryGetSdkResponseData(fiscalResponse, out var fiscalData) ||
                !TryReadFiscalInformation(fiscalData, out var fiscalInformation))
                return Outcome(SigningConnectionTestStatus.Indeterminate);

            // Presence of a valid fiscal-info response establishes auth, not portal registration/activation.
            return Outcome(SigningConnectionTestStatus.Authenticated, fiscalInformation);
        }
        catch (AuthorityRejectedException) { return Outcome(SigningConnectionTestStatus.AuthenticationRejected); }
        catch (AuthorityUnavailableException) { return Outcome(SigningConnectionTestStatus.EndpointUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return Outcome(SigningConnectionTestStatus.TimedOut); }
        catch (OperationCanceledException) { throw; }
        catch (KeyProtectionException) { return Outcome(SigningConnectionTestStatus.VaultUnavailable); }
        catch (HttpRequestException) { return Outcome(SigningConnectionTestStatus.EndpointUnavailable); }
        catch (JsonException) { return Outcome(SigningConnectionTestStatus.Indeterminate); }
        catch (CryptographicException) { return Outcome(SigningConnectionTestStatus.KeyNotReady); }
    }

    private static bool TryProperty(JsonElement root, string name, out JsonElement found)
    {
        found = default;
        if (root.ValueKind != JsonValueKind.Object) return false;
        foreach (var prop in root.EnumerateObject())
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            { found = prop.Value; return true; }
        return false;
    }

    internal static bool TryGetPacketData(JsonDocument response, string expectedType, out JsonElement data)
    {
        data = default;
        var root = response.RootElement;
        if (TryProperty(root, "errors", out var errors) &&
            errors.ValueKind != JsonValueKind.Null &&
            (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() != 0))
            return false;
        if (!TryProperty(root, "result", out var result) ||
            !TryProperty(result, "packetType", out var packetType) ||
            packetType.ValueKind != JsonValueKind.String || !IsExpectedPacketType(packetType.GetString(), expectedType) ||
            !TryProperty(result, "data", out data) || data.ValueKind == JsonValueKind.Null)
            return false;
        return true;
    }

    internal static bool TryGetFiscalInformationData(JsonDocument response, out JsonElement data) =>
        TryGetPacketData(response, FiscalInformationResponsePacketType, out data) ||
        TryGetPacketData(response, "GET_FISCAL_INFORMATION", out data);

    // TaxCollectData.Library 0.0.30 returns Result.Data and only rejects non-empty Errors;
    // it does not assert a response packet type.
    internal static bool TryGetSdkResponseData(JsonDocument response, out JsonElement data)
    {
        data = default;
        var root = response.RootElement;
        if (TryProperty(root, "errors", out var errors) &&
            errors.ValueKind != JsonValueKind.Null &&
            (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() != 0))
            return false;
        return TryProperty(root, "result", out var result) &&
            result.ValueKind == JsonValueKind.Object &&
            TryProperty(result, "data", out data) &&
            data.ValueKind != JsonValueKind.Null;
    }

    internal static bool TryReadFiscalInformation(JsonElement data, out FiscalInformationSnapshot information)
    {
        information = default!;
        if (data.ValueKind != JsonValueKind.Object ||
            !TryProperty(data, "nameTrade", out var nameTrade) ||
            !TryProperty(data, "fiscalStatus", out var fiscalStatus) ||
            !TryProperty(data, "economicCode", out var economicCode) ||
            !TryProperty(data, "nationalId", out var nationalId))
            return false;

        var values = new[] { nameTrade, fiscalStatus, economicCode, nationalId }
            .Select(x => x.ValueKind is JsonValueKind.String or JsonValueKind.Number ? x.ToString() : "")
            .ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) || values.Any(x => x.Length > 256))
            return false;
        information = new FiscalInformationSnapshot(values[0], values[1], values[2], values[3]);
        return true;
    }

    private static bool IsExpectedPacketType(string? actual, string expected) =>
        string.Equals(actual, expected, StringComparison.Ordinal) ||
        expected == FiscalInformationResponsePacketType &&
        actual == "GET_FISCAL_INFORMATION";

    private async Task<JsonDocument> SendAsync(Uri destination, string type, string fiscalId, object? data,
        SensitiveBytes privateKey, byte[] expectedSpki, string? accessToken, CancellationToken ct)
    {
        var trace = Guid.NewGuid().ToString("D");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signHeaders = new Dictionary<string, string> {
            ["requestTraceId"] = trace, ["timestamp"] = timestamp
        };
        if (accessToken is not null) signHeaders["Authorization"] = "Bearer " + accessToken;
        // Exact DTO property order/types from PacketDtoWithoutSignatureKeyId of SDK 0.0.30.
        var packet = new {
            Uid = Guid.NewGuid().ToString("D"), PacketType = type, Retry = false, Data = data,
            EncryptionKeyId = (string?)null, SymmetricKey = (string?)null, Iv = (string?)null,
            FiscalId = fiscalId, DataSignature = (string?)null
        };
        var signature = new LegacyOfflineSigningEngine().SignObject(privateKey, expectedSpki, packet,
            LegacySigningPurpose.Transfer, signHeaders).SignatureBase64;
        using var request = new HttpRequestMessage(HttpMethod.Post, destination) {
            Content = JsonContent.Create(new { Signature = signature, SignatureKeyId = (string?)null, Packet = new { packet.Uid, packet.PacketType, packet.Retry, packet.Data, packet.EncryptionKeyId, packet.SymmetricKey, packet.Iv, packet.FiscalId, packet.DataSignature, SignatureKeyId = (string?)null } },
                options: JsonOptions)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("requestTraceId", trace);
        request.Headers.TryAddWithoutValidation("timestamp", timestamp);
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new AuthorityRejectedException();
        if (!response.IsSuccessStatusCode) throw new AuthorityUnavailableException();
        if (response.Content.Headers.ContentLength is > 262144) throw new JsonException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var bounded = new MemoryStream();
        var buffer = new byte[8192];
        while (true) {
            var n = await stream.ReadAsync(buffer, ct);
            if (n == 0) break;
            if (bounded.Length + n > 262144) throw new JsonException();
            bounded.Write(buffer, 0, n);
        }
        bounded.Position = 0;
        try
        {
            var parsed = await JsonDocument.ParseAsync(bounded, new JsonDocumentOptions { MaxDepth = 16 }, ct);
            logger.LogInformation("Tax authority response inspected for {PacketType}: {Summary}",
                type, DescribeResponse(parsed));
            return parsed;
        }
        finally {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(bounded.GetBuffer().AsSpan(0, (int)bounded.Length));
        }
    }

    private static string DescribeResponse(JsonDocument response)
    {
        var root = response.RootElement;
        var errorState = TryProperty(root, "errors", out var errors)
            ? errors.ValueKind == JsonValueKind.Array ? $"array:{errors.GetArrayLength()}" : errors.ValueKind.ToString()
            : "missing";
        var packetType = "missing";
        var dataKind = "missing";
        if (TryProperty(root, "result", out var result) && result.ValueKind == JsonValueKind.Object)
        {
            if (TryProperty(result, "packetType", out var type) && type.ValueKind == JsonValueKind.String)
                packetType = type.GetString() ?? "empty";
            if (TryProperty(result, "data", out var data))
                dataKind = data.ValueKind.ToString();
        }
        return $"packetType={packetType}; data={dataKind}; errors={errorState}";
    }

    private sealed class AuthorityRejectedException : Exception { }
    private sealed class AuthorityUnavailableException : Exception { }
}
