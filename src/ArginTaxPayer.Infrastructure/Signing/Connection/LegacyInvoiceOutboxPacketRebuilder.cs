using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

internal static class LegacyInvoiceOutboxPacketRebuilder
{
    private const int MaxPayloadBytes = 3_500_000;

    internal static LegacyInvoiceSubmissionPacket Rebuild(
        string outboxPayloadJson, SensitiveBytes signingKey,
        ReadOnlyMemory<byte> expectedPublicSpki, string accessToken,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (outboxPayloadJson is not { Length: > 2 and <= MaxPayloadBytes })
            throw new ArgumentException("Invalid legacy Outbox payload.", nameof(outboxPayloadJson));

        using var payload = JsonDocument.Parse(outboxPayloadJson,
            new JsonDocumentOptions { MaxDepth = 24 });
        var root = payload.RootElement;
        var schema = RequiredString(root, "schema");
        if (schema != "phase10.sandbox.legacy.invoice.dispatch.v1" ||
            RequiredString(root, "environment") != "Sandbox")
            throw new ArgumentException("Invalid legacy Outbox schema.", nameof(outboxPayloadJson));

        var operationId = RequiredGuid(root, "operationId");
        var customerUserId = RequiredGuid(root, "customerUserId");
        var taxpayerId = RequiredGuid(root, "taxpayerId");
        var fiscalId = RequiredString(root, "fiscalTaxId");
        var requestJson = RequiredString(root, "requestJson");
        var storedSignature = RequiredString(root, "transferSignature");
        var storedHash = RequiredString(root, "requestSha256");
        if (root.TryGetProperty("accessTokenStored", out var stored) &&
            stored.ValueKind != JsonValueKind.False)
            throw new ArgumentException("Legacy access token must not be stored.", nameof(outboxPayloadJson));

        var expectedHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(requestJson))).ToLowerInvariant();
        if (!string.Equals(storedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Legacy Outbox request hash mismatch.", nameof(outboxPayloadJson));

        using var request = JsonDocument.Parse(requestJson,
            new JsonDocumentOptions { MaxDepth = 24 });
        var requestRoot = request.RootElement;
        if (!TryProperty(requestRoot, "signature", out var signature) ||
            signature.ValueKind != JsonValueKind.String ||
            !string.Equals(signature.GetString(), storedSignature, StringComparison.Ordinal) ||
            !TryProperty(requestRoot, "packets", out var packets) ||
            packets.ValueKind != JsonValueKind.Array || packets.GetArrayLength() != 1)
            throw new ArgumentException("Invalid legacy Outbox request envelope.", nameof(outboxPayloadJson));
        var packet = packets[0];
        if (!TryProperty(packet, "packetType", out var packetType) ||
            packetType.ValueKind != JsonValueKind.String ||
            packetType.GetString() != "INVOICE" ||
            !TryProperty(packet, "data", out var data) ||
            data.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Invalid legacy invoice packet.", nameof(outboxPayloadJson));

        return LegacyInvoiceSubmissionPacketBuilder.Build(
            operationId, customerUserId, taxpayerId, fiscalId,
            data.GetRawText(), signingKey, expectedPublicSpki, accessToken,
            Guid.NewGuid(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                .ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!TryProperty(root, name, out var value) || value.ValueKind != JsonValueKind.String ||
            value.GetString() is not { Length: > 0 and <= 3_000_000 } text)
            throw new ArgumentException("Invalid legacy Outbox field: " + name);
        return text;
    }

    private static Guid RequiredGuid(JsonElement root, string name)
    {
        var text = RequiredString(root, name);
        if (!Guid.TryParse(text, out var value) || value == Guid.Empty)
            throw new ArgumentException("Invalid legacy Outbox identifier: " + name);
        return value;
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
}
