using System.Text.Json;

namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Phase10 Step13 response adapter for the archived SDK 2.0.32 batch shape.
/// This is a conservative initial-ack decoder, NOT a legal acceptance check
/// and NOT an authority-compatible live HTTP transport.
/// </summary>
public enum InvoiceAuthorityResponseClassification
{
    InitialReceipt = 1,
    DefinitiveRejection = 2,
    Indeterminate = 3
}

public sealed record InvoiceAuthorityResponseDecision(
    InvoiceAuthorityResponseClassification Classification, string SafeCode,
    string? ReferenceNumber = null)
{
    public bool IsFiscalAccepted => false;
    public bool MayAutomaticallyRetry => false;
}

/// <summary>
/// Input is provider-untrusted JSON. On malformed/incomplete/ambiguous data,
/// returns Indeterminate without reflecting provider text. A response reference
/// is accepted only for exactly one packet matching the expected UID/type.
/// Terminal rejection is intentionally never inferred without a verified
/// authority error-code contract.
/// </summary>
public static class Phase10AuthorityBatchResponseParser
{
    public static InvoiceAuthorityResponseDecision Parse(
        string? json, string expectedPacketUid, string expectedPacketType)
    {
        static InvoiceAuthorityResponseDecision Unknown() =>
            new(InvoiceAuthorityResponseClassification.Indeterminate,
                "invoice.dispatch.response_indeterminate");
        if (string.IsNullOrWhiteSpace(json) || json.Length > 262144 ||
            !IsOpaqueId(expectedPacketUid, 128) ||
            !IsPacketType(expectedPacketType, 80))
            return Unknown();

        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 16,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            var root = doc.RootElement;
            if (HasDuplicateNames(root)) return Unknown();
            if (root.ValueKind != JsonValueKind.Object ||
                !TryUnique(root, "result", out var packets) ||
                packets.ValueKind != JsonValueKind.Array ||
                packets.GetArrayLength() != 1 ||
                (TryUnique(root, "errors", out var errors) &&
                 errors.ValueKind != JsonValueKind.Null &&
                 (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() != 0)))
                return Unknown();
            var packet = packets[0];
            if (HasDuplicateNames(packet)) return Unknown();
            if (packet.ValueKind != JsonValueKind.Object ||
                !TryUnique(packet, "uid", out var uid) ||
                !TryUnique(packet, "packetType", out var type) ||
                !TryUnique(packet, "referenceNumber", out var reference) ||
                uid.ValueKind != JsonValueKind.String ||
                type.ValueKind != JsonValueKind.String ||
                reference.ValueKind != JsonValueKind.String ||
                !string.Equals(uid.GetString(), expectedPacketUid, StringComparison.Ordinal) ||
                !string.Equals(type.GetString(), expectedPacketType, StringComparison.Ordinal) ||
                !IsOpaqueId(reference.GetString(), 128))
                return Unknown();

            // Additional error/data status fields must not suggest acceptance.
            // We intentionally treat the reference as receipt-only, not SUCCESS.
            if (TryUnique(packet, "errors", out var packetErrors) &&
                packetErrors.ValueKind != JsonValueKind.Null &&
                (packetErrors.ValueKind != JsonValueKind.Array ||
                 packetErrors.GetArrayLength() != 0))
                return Unknown();
            return new(InvoiceAuthorityResponseClassification.InitialReceipt,
                "invoice.dispatch.initial_receipt_recorded", reference.GetString());
        }
        catch (JsonException)
        {
            return Unknown();
        }
        catch (InvalidOperationException)
        {
            return Unknown();
        }
    }

    // Duplicate or case-varied aliases are rejected instead of depending on
    // serializer last-property-wins behavior.
    private static bool HasDuplicateNames(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object) return false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.EnumerateObject())
            if (!names.Add(prop.Name)) return true;
        return false;
    }

    private static bool TryUnique(JsonElement element, string field, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        var found = false;
        foreach (var p in element.EnumerateObject())
        {
            if (!p.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) continue;
            if (found) return false;
            found = true;
            value = p.Value;
        }
        return found;
    }

    private static bool IsOpaqueId(string? value, int max) =>
        value is { Length: > 0 } && value.Length <= max &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or '-' or '_');

    private static bool IsPacketType(string? value, int max) =>
        value is { Length: > 0 } && value.Length <= max &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or '-' or '_' or '.');
}
