using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
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

    internal static LegacyInvoiceSubmissionPacket BuildEncrypted(
        Guid operationId, Guid customerUserId, Guid taxpayerId, string fiscalTaxId,
        string invoiceJson, SensitiveBytes signingKey,
        ReadOnlyMemory<byte> expectedPublicSpki, string accessToken,
        Guid requestTraceId, string timestamp, string sandboxMemoryClientId,
        LegacySandboxEncryptionKey authorityEncryptionKey)
    {
        if (sandboxMemoryClientId is not { Length: 6 } ||
            sandboxMemoryClientId.Any(c=>!char.IsAsciiLetterOrDigit(c)) ||
            operationId==Guid.Empty || customerUserId==Guid.Empty ||
            taxpayerId==Guid.Empty || requestTraceId==Guid.Empty ||
            fiscalTaxId is not {Length:22} ||
            !fiscalTaxId.All(char.IsAsciiLetterOrDigit) ||
            accessToken is not {Length: > 0 and <= 16384} ||
            accessToken.Any(char.IsWhiteSpace) ||
            timestamp is not { Length: > 0 and <= 32 } ||
            timestamp.Any(c=>!char.IsAsciiDigit(c)))
            throw new ArgumentException("Invalid Legacy encrypted invoice identity.");
        using var document=JsonDocument.Parse(invoiceJson);
        var original=document.RootElement;
        if(original.ValueKind!=JsonValueKind.Object ||
           !original.TryGetProperty("header",out var header) ||
           !header.TryGetProperty("taxid",out var taxid) ||
           taxid.GetString()!=fiscalTaxId)
            throw new ArgumentException("Legacy invoice tax ID mismatch.");
        var signing=new LegacyOfflineSigningEngine();
        var contentSignature=signing.SignObject(signingKey,expectedPublicSpki,
            JObject.Parse(invoiceJson),LegacySigningPurpose.Content).SignatureBase64;
        var encrypted=LegacySandboxInvoiceEncryption.Encrypt(original,authorityEncryptionKey);
        var packet=new {
            uid=operationId.ToString("D"),packetType="INVOICE.V01",retry=false,
            data=encrypted.CiphertextBase64,
            encryptionKeyId=encrypted.EncryptionKeyId,
            symmetricKey=encrypted.WrappedKeyBase64,iv=encrypted.IvHex,
            fiscalId=sandboxMemoryClientId,dataSignature=contentSignature,
            signatureKeyId=(string?)null
        };
        var headers=new Dictionary<string,string>{
            ["requestTraceId"]=requestTraceId.ToString("D"),
            ["timestamp"]=timestamp,["Authorization"]="Bearer "+accessToken
        };
        var signature=SignAsyncPacket(packet,signingKey,expectedPublicSpki,headers);
        var body=new { signature,signatureKeyId=(string?)null,packets=new[]{packet} };
        var requestJson=JsonSerializer.Serialize(body,JsonOptions);
        return new(operationId,customerUserId,taxpayerId,fiscalTaxId,
            requestJson,signature,requestTraceId.ToString("D"),timestamp,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)))
                .ToLowerInvariant());
    }

    internal static LegacyInvoiceSubmissionPacket ReSignEncrypted(
        Guid operationId,Guid customerUserId,Guid taxpayerId,string fiscalTaxId,
        string encryptedData,string encryptionKeyId,string wrappedKey,string ivHex,
        string contentSignature,string memoryCode,SensitiveBytes signingKey,
        ReadOnlyMemory<byte> expectedPublicSpki,string accessToken,
        Guid requestTraceId,string timestamp)
    {
        if(memoryCode is not {Length:6} ||
           memoryCode.Any(c=>!char.IsAsciiLetterOrDigit(c)) ||
           encryptedData is not {Length: > 16} ||
           encryptionKeyId is not {Length: > 0 and <=128} ||
           wrappedKey is not {Length: > 16} ||
           ivHex is not {Length:32} ||
           contentSignature is not {Length: >16})
            throw new ArgumentException("Invalid encrypted Legacy packet.");
        var packet=new{
            uid=operationId.ToString("D"),packetType="INVOICE.V01",retry=false,
            data=encryptedData,encryptionKeyId,symmetricKey=wrappedKey,
            iv=ivHex,fiscalId=memoryCode,dataSignature=contentSignature,
            signatureKeyId=(string?)null
        };
        var headers=new Dictionary<string,string>{
            ["requestTraceId"]=requestTraceId.ToString("D"),["timestamp"]=timestamp,
            ["Authorization"]="Bearer "+accessToken
        };
        var signature=SignAsyncPacket(packet,signingKey,expectedPublicSpki,headers);
        var requestJson=JsonSerializer.Serialize(new{
            signature,signatureKeyId=(string?)null,packets=new[]{packet}
        },JsonOptions);
        return new(operationId,customerUserId,taxpayerId,fiscalTaxId,requestJson,
            signature,requestTraceId.ToString("D"),timestamp,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)))
                .ToLowerInvariant());
    }

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
            packetType = "INVOICE.V01",
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
        var signature = SignAsyncPacket(packet, signingKey, expectedPublicSpki, headers);
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

    private static string SignAsyncPacket(object packet, SensitiveBytes signingKey,
        ReadOnlyMemory<byte> expectedPublicSpki, IReadOnlyDictionary<string,string> headers)
    {
        // TaxPayerBackend ObjectTransferApi.GetAsyncSendData signs a packet collection.
        // Its certificate-free adapter omits SignatureKeyId from the signed packet.
        // Serialize with STJ first so JsonElement invoice values survive Newtonsoft normalization.
        var signingPacket = JObject.Parse(JsonSerializer.Serialize(packet, JsonOptions));
        signingPacket.Remove("signatureKeyId");
        return new LegacyOfflineSigningEngine().SignObject(signingKey, expectedPublicSpki,
            new object[] { signingPacket }, LegacySigningPurpose.Transfer, headers).SignatureBase64;
    }
}
