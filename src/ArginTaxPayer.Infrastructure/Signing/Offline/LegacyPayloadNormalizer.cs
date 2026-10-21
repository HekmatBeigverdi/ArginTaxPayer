using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StjJsonException = System.Text.Json.JsonException;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Offline;

internal enum LegacySigningMode { ObjectV030 = 1, SimpleV1V030 = 2 }
internal enum LegacySigningPurpose { Content = 1, Transfer = 2 }

internal sealed class LegacySigningInputException : Exception
{
    public LegacySigningInputException() : base("Legacy signing input rejected.") { }
}

/// <summary>Reproduces source 0.0.30 object and pre-serialized JSON normalization paths.</summary>
internal static class LegacyPayloadNormalizer
{
    public const int MaximumPayloadBytes = 8 * 1024 * 1024;
    private sealed record PacketsCollectionWrapper(IEnumerable<object> Packets);
    private static readonly JsonSerializerOptions SimpleV1Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly HashSet<string> SignedHeaders = new(StringComparer.Ordinal)
    { "signature", "signatureKeyId", "requestTraceId", "timestamp" };

    public static byte[] SerializePayload(object payload, LegacySigningMode mode)
    {
        ArgumentNullException.ThrowIfNull(payload);
        try
        {
            return mode switch
            {
                LegacySigningMode.ObjectV030 => SerializeObjectPayload(payload),
                LegacySigningMode.SimpleV1V030 => JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), SimpleV1Options),
                _ => throw new LegacySigningInputException()
            };
        }
        catch (LegacySigningInputException) { throw; }
        catch (Exception error) when (error is StjJsonException or Newtonsoft.Json.JsonException or
            NotSupportedException or ArgumentException)
        { throw new LegacySigningInputException(); }
    }

    private static byte[] SerializeObjectPayload(object payload)
    {
        var token = ToObjectToken(payload);
        return new UTF8Encoding(false, true).GetBytes(token.ToString(Formatting.None));
    }

    public static SensitiveBytes NormalizeObjectPayload(object payload, LegacySigningPurpose purpose,
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyDictionary<string, string>? customHeaders = null, string tokenHeaderName = "Authorization")
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!Enum.IsDefined(purpose) || string.IsNullOrEmpty(tokenHeaderName) || tokenHeaderName.Length > 128)
            throw new LegacySigningInputException();
        byte[]? result = null;
        try
        {
            result = NormalizeObjectRoot(ToObjectToken(payload), purpose, headers, customHeaders, tokenHeaderName);
            if (result.Length is < 1 or > MaximumPayloadBytes) throw new LegacySigningInputException();
            var owned = SensitiveBytes.TakeOwnership(result);
            result = null;
            return owned;
        }
        catch (LegacySigningInputException) { throw; }
        catch (Exception error) when (error is Newtonsoft.Json.JsonException or DecoderFallbackException or
            FormatException or OverflowException or InvalidOperationException or ArgumentException)
        { throw new LegacySigningInputException(); }
        finally { if (result is not null) CryptographicOperations.ZeroMemory(result); }
    }

    private static JObject ToObjectToken(object payload)
    {
        object value = payload is IEnumerable<object> collection && payload is not string
            ? new PacketsCollectionWrapper(collection) : payload;
        return JObject.FromObject(value);
    }

    public static SensitiveBytes Normalize(ReadOnlyMemory<byte> payloadJson, LegacySigningMode mode,
        LegacySigningPurpose purpose, IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyDictionary<string, string>? customHeaders = null, string tokenHeaderName = "Authorization")
    {
        if (!Enum.IsDefined(mode) || !Enum.IsDefined(purpose) || payloadJson.Length is < 1 or > MaximumPayloadBytes ||
            string.IsNullOrEmpty(tokenHeaderName) || tokenHeaderName.Length > 128)
            throw new LegacySigningInputException();
        byte[]? result = null;
        try
        {
            result = mode switch
            {
                LegacySigningMode.ObjectV030 => NormalizeObject(payloadJson, purpose, headers,
                    customHeaders, tokenHeaderName),
                LegacySigningMode.SimpleV1V030 => NormalizeSimple(payloadJson, purpose, headers, tokenHeaderName),
                _ => throw new LegacySigningInputException()
            };
            if (result.Length is < 1 or > MaximumPayloadBytes) throw new LegacySigningInputException();
            var owned = SensitiveBytes.TakeOwnership(result);
            result = null;
            return owned;
        }
        catch (LegacySigningInputException) { throw; }
        catch (Exception error) when (error is StjJsonException or Newtonsoft.Json.JsonException or DecoderFallbackException or FormatException or
            OverflowException or InvalidOperationException or ArgumentException)
        { throw new LegacySigningInputException(); }
        finally { if (result is not null) CryptographicOperations.ZeroMemory(result); }
    }

    private static byte[] NormalizeObject(ReadOnlyMemory<byte> json, LegacySigningPurpose purpose,
        IReadOnlyDictionary<string, string>? headers, IReadOnlyDictionary<string, string>? customHeaders,
        string tokenHeaderName)
    {
        var text = new UTF8Encoding(false, true).GetString(json.Span);
        var root = JObject.Parse(text);
        return NormalizeObjectRoot(root, purpose, headers, customHeaders, tokenHeaderName);
    }

    private static byte[] NormalizeObjectRoot(JObject root, LegacySigningPurpose purpose,
        IReadOnlyDictionary<string, string>? headers, IReadOnlyDictionary<string, string>? customHeaders,
        string tokenHeaderName)
    {
        var flattened = new Dictionary<string, (string Value, bool EscapeHashes)>(StringComparer.Ordinal);
        Flatten(root, null, flattened);
        if (purpose == LegacySigningPurpose.Transfer && headers is not null)
            AddObjectHeaders(flattened, headers, customHeaders, tokenHeaderName);
        var keys = flattened.Keys.ToList();
        keys.Sort(); // List<string>.Sort() matches the legacy source's default comparer.
        if (keys.Count == 0) throw new LegacySigningInputException();
        var parts = new string[keys.Count];
        for (var i = 0; i < keys.Count; i++)
        {
            var (value, escapeHashes) = flattened[keys[i]];
            parts[i] = string.IsNullOrEmpty(value) ? "#" : escapeHashes
                ? value.Replace("#", "##", StringComparison.Ordinal) : value;
        }
        return new UTF8Encoding(false, true).GetBytes(string.Join('#', parts));
    }

    private static void Flatten(JToken element, string? root,
        Dictionary<string, (string Value, bool EscapeHashes)> output)
    {
        switch (element.Type)
        {
            case JTokenType.Object:
                foreach (var property in ((JObject)element).Properties())
                    Flatten(property.Value, Join(root, property.Name), output);
                break;
            case JTokenType.Array:
                var index = 0;
                foreach (var value in (JArray)element)
                    Flatten(value, Join(root, "E" + index++.ToString(CultureInfo.InvariantCulture)), output);
                break;
            case JTokenType.Null:
            case JTokenType.Undefined:
                Add(root, "#", output, escapeHashes: false);
                break;
            case JTokenType.Boolean:
                Add(root, element.ToString().ToLowerInvariant(), output);
                break;
            case JTokenType.Integer:
            case JTokenType.Float:
            case JTokenType.String:
            case JTokenType.Date:
                Add(root, element.ToString(), output);
                break;
            default:
                Add(root, "#", output, escapeHashes: false);
                break;
        }
    }

    private static void AddObjectHeaders(Dictionary<string, (string Value, bool EscapeHashes)> map,
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyDictionary<string, string>? customHeaders, string tokenHeaderName)
    {
        foreach (var header in headers)
        {
            if (customHeaders?.ContainsKey(header.Key) == true) continue;
            if (string.Equals(header.Key, tokenHeaderName, StringComparison.Ordinal) && header.Value.Length > 7)
                Add("Authorization", header.Value[7..], map);
            else if (SignedHeaders.Contains(header.Key))
                Add(header.Key, header.Value, map);
        }
    }

    private static byte[] NormalizeSimple(ReadOnlyMemory<byte> json, LegacySigningPurpose purpose,
        IReadOnlyDictionary<string, string>? headers, string tokenHeaderName)
    {
        using var _ = JsonDocument.Parse(json); // Reject malformed UTF-8/JSON before signing.
        var suffix = new List<string>(3);
        if (purpose == LegacySigningPurpose.Transfer && headers is { Count: > 0 })
        {
            AddHeader(tokenHeaderName);
            AddHeader("requestTraceId");
            AddHeader("timestamp");
        }
        var encoding = new UTF8Encoding(false, true);
        var byteCount = json.Length;
        foreach (var value in suffix) byteCount = checked(byteCount + encoding.GetByteCount(value));
        if (byteCount is < 1 or > MaximumPayloadBytes) throw new LegacySigningInputException();
        var output = new byte[byteCount];
        try
        {
            json.Span.CopyTo(output);
            var offset = json.Length;
            foreach (var value in suffix) offset += encoding.GetBytes(value, output.AsSpan(offset));
            return output;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(output);
            throw;
        }

        void AddHeader(string name)
        {
            if (headers!.TryGetValue(name, out var value)) suffix.Add(value);
        }
    }

    private static void Add(string? key, string value,
        Dictionary<string, (string Value, bool EscapeHashes)> output, bool escapeHashes = true)
    {
        if (string.IsNullOrWhiteSpace(key) || !output.TryAdd(key, (value, escapeHashes)))
            throw new LegacySigningInputException();
    }
    private static string Join(string? root, string key) => string.IsNullOrWhiteSpace(root) ? key : $"{root}.{key}";
}
