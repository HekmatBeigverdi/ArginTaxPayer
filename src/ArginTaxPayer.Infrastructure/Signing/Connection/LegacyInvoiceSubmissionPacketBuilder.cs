using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

internal sealed record LegacyInvoiceSubmissionPacket(
    Guid OperationId,
    Guid CustomerUserId,
    Guid TaxpayerId,
    string FiscalTaxId,
    string RequestJson,
    string TransferSignature,
    string RequestTraceId,
    string Timestamp,
    string RequestSha256);

/// <summary>
/// Builds the certificate-free SELF_TSP invoice envelope. The signature is
/// calculated over the exact legacy-normalized packet and then placed in the
/// exact request body sent by the transport. This class performs no HTTP and
/// never persists or returns private key material.
/// </summary>
internal static class LegacyInvoiceSubmissionPacketBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    internal static LegacyInvoiceSubmissionPacket Build(
        Guid operationId, Guid customerUserId, Guid taxpayerId, string fiscalId,
        string invoiceJson, SensitiveBytes signingKey, ReadOnlyMemory<byte> expectedPublicSpki,
        string accessToken, Guid requestTraceId, string timestamp,
        string? sandboxMemoryClientId = null)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty || taxpayerId == Guid.Empty ||
            requestTraceId == Guid.Empty || fiscalId is not { Length: >= 6 and <= 32 } ||
            fiscalId.Any(c => !char.IsAsciiLetterOrDigit(c)) ||
            invoiceJson is not { Length: > 2 and <= 1_048_576 } ||
            accessToken is not { Length: > 0 and <= 16_384 } || accessToken.Any(char.IsWhiteSpace) ||
            timestamp is not { Length: > 0 and <= 32 } || timestamp.Any(c => !char.IsAsciiDigit(c)))
            throw new ArgumentException("Invalid legacy invoice packet input.");
        if (sandboxMemoryClientId is not null &&
            (sandboxMemoryClientId.Length != 6 ||
             sandboxMemoryClientId.Any(c => !char.IsAsciiLetterOrDigit(c))))
            throw new ArgumentException("Invalid Sandbox memory client ID.");
        ArgumentNullException.ThrowIfNull(signingKey);
        if (expectedPublicSpki.Length is < 1 or > 4096)
            throw new ArgumentException("Invalid legacy signing public key.", nameof(expectedPublicSpki));

        using var invoice = JsonDocument.Parse(invoiceJson);
        if (invoice.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Legacy invoice payload must be an object.", nameof(invoiceJson));

        var packet = new
        {
            uid = operationId.ToString("D"),
            packetType = "INVOICE",
            retry = false,
            data = invoice.RootElement.Clone(),
            encryptionKeyId = (string?)null,
            symmetricKey = (string?)null,
            iv = (string?)null,
            fiscalId = sandboxMemoryClientId ?? fiscalId,
            dataSignature = (string?)null,
            signatureKeyId = (string?)null
        };
        var headers = new Dictionary<string, string>
        {
            ["requestTraceId"] = requestTraceId.ToString("D"),
            ["timestamp"] = timestamp,
            ["Authorization"] = "Bearer " + accessToken
        };
        var signature = new LegacyOfflineSigningEngine().SignObject(
            signingKey, expectedPublicSpki, packet, LegacySigningPurpose.Transfer, headers)
            .SignatureBase64;
        var body = new
        {
            signature,
            signatureKeyId = (string?)null,
            packets = new[] { packet }
        };
        var requestJson = JsonSerializer.Serialize(body, JsonOptions);
        return new(operationId, customerUserId, taxpayerId, fiscalId, requestJson, signature,
            requestTraceId.ToString("D"), timestamp,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson))).ToLowerInvariant());
    }
}
