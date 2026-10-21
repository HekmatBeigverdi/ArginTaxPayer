using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09OfflineSigningNormalizerTests
{
    [Fact]
    public void Object_mode_flattens_and_sorts_properties_escapes_hashes_and_lowercases_booleans()
    {
        using var actual = Normalize("{\"z\":true,\"a\":\"x#y\",\"skip\":{},\"empty\":\"\",\"nil\":null}");
        Assert.Equal("x##y###true", System.Text.Encoding.UTF8.GetString(actual.Bytes.Span));
    }

    [Fact]
    public void Object_mode_does_not_escape_the_sdk_null_marker_but_escapes_literal_hashes()
    {
        using var nullMarker = Normalize("{\"value\":null}");
        using var literalHash = Normalize("{\"value\":\"#\"}");
        Assert.Equal("#", System.Text.Encoding.UTF8.GetString(nullMarker.Bytes.Span));
        Assert.Equal("##", System.Text.Encoding.UTF8.GetString(literalHash.Bytes.Span));
    }

    [Fact]
    public void Object_mode_uses_E_indexed_array_paths_and_emits_null_leaves()
    {
        using var actual = Normalize("{\"items\":[{\"value\":2},null]}");
        Assert.Equal("2##", System.Text.Encoding.UTF8.GetString(actual.Bytes.Span));
    }

    [Fact]
    public void Object_transfer_includes_only_sdk_headers_and_removes_seven_token_prefix_characters()
    {
        using var actual = Normalize("{\"value\":\"payload\"}", LegacySigningPurpose.Transfer,
            new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer secret-token", ["requestTraceId"] = "trace",
                ["timestamp"] = "1700000000000", ["signature"] = "sig", ["x-custom"] = "hidden"
            }, new Dictionary<string, string> { ["x-custom"] = "value" });
        Assert.Equal("secret-token#trace#sig#1700000000000#payload", System.Text.Encoding.UTF8.GetString(actual.Bytes.Span));
    }

    [Fact]
    public void Object_content_ignores_transfer_headers()
    {
        using var actual = Normalize("{\"value\":\"payload\"}", LegacySigningPurpose.Content,
            new Dictionary<string, string> { ["Authorization"] = "Bearer token", ["requestTraceId"] = "trace" });
        Assert.Equal("payload", System.Text.Encoding.UTF8.GetString(actual.Bytes.Span));
    }

    [Fact]
    public void Object_custom_headers_are_excluded_and_empty_root_is_rejected()
    {
        using var withHeader = Normalize("{\"value\":\"payload\"}", LegacySigningPurpose.Transfer,
            new Dictionary<string, string> { ["x-private"] = "do-not-sign" },
            new Dictionary<string, string> { ["x-private"] = "do-not-sign" });
        Assert.Equal("payload", System.Text.Encoding.UTF8.GetString(withHeader.Bytes.Span));
        Assert.Throws<LegacySigningInputException>(() => Normalize("{}"));
    }

    [Fact]
    public void Duplicate_flattened_path_or_reserved_header_is_rejected()
    {
        Assert.Throws<LegacySigningInputException>(() => Normalize("{\"a.b\":1,\"a\":{\"b\":2}}"));
        Assert.Throws<LegacySigningInputException>(() => Normalize("{\"Authorization\":\"payload\"}",
            LegacySigningPurpose.Transfer, new Dictionary<string, string> { ["Authorization"] = "Bearer token" }));
    }

    [Fact]
    public void Simple_v1_uses_camel_case_string_enums_then_concatenates_headers_in_sdk_order()
    {
        var bytes = LegacyPayloadNormalizer.SerializePayload(new SimpleFixture
        {
            State = FixtureState.Ready,
            Amount = 10,
            Name = "مؤدی"
        }, LegacySigningMode.SimpleV1V030);
        using var actualBuffer = LegacyPayloadNormalizer.Normalize(bytes, LegacySigningMode.SimpleV1V030,
            LegacySigningPurpose.Transfer, new Dictionary<string, string>
            {
                ["timestamp"] = "t", ["requestTraceId"] = "r", ["Authorization"] = "Bearer token",
                ["signature"] = "ignored", ["signatureKeyId"] = "ignored"
            });
        Assert.Equal(System.Text.Encoding.UTF8.GetString(bytes) + "Bearer tokenrt", System.Text.Encoding.UTF8.GetString(actualBuffer.Bytes.Span));
        Assert.Contains("\"state\":\"Ready\"", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Object_mode_formats_decimal_using_current_culture()
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            using var actual = Normalize("{\"amount\":1.5}");
            Assert.Equal("1,5", System.Text.Encoding.UTF8.GetString(actual.Bytes.Span));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void Object_sdk_collection_serialization_adds_packets_wrapper()
    {
        var payload = new object[] { new PacketFixture { Name = "one" } };
        var json = LegacyPayloadNormalizer.SerializePayload(payload, LegacySigningMode.ObjectV030);
        Assert.Equal("{\"Packets\":[{\"Name\":\"one\"}]}", System.Text.Encoding.UTF8.GetString(json));
    }

    [Fact]
    public void Object_mode_honors_newtonsoft_contract_attributes_like_sdk_JObject_FromObject()
    {
        var bytes = LegacyPayloadNormalizer.SerializePayload(new ObjectContractFixture { Name = "value" },
            LegacySigningMode.ObjectV030);
        Assert.Equal("{\"legacy_name\":\"value\"}", System.Text.Encoding.UTF8.GetString(bytes));
        using var actual = LegacyPayloadNormalizer.Normalize(bytes, LegacySigningMode.ObjectV030,
            LegacySigningPurpose.Content);
        Assert.Equal("value", System.Text.Encoding.UTF8.GetString(actual.Bytes.Span));
    }

    [Fact]
    public void Object_mode_preserves_source_token_types_for_dates_and_high_precision_decimals()
    {
        using var actual = LegacyPayloadNormalizer.NormalizeObjectPayload(new PrecisionFixture
        {
            Amount = 1.234567890123456789m,
            DateText = "2025-01-01T00:00:00Z"
        }, LegacySigningPurpose.Content);
        Assert.Equal("1.234567890123456789#2025-01-01T00:00:00Z",
            System.Text.Encoding.UTF8.GetString(actual.Bytes.Span));
    }

    [Fact]
    public void Simple_v1_content_is_json_only_and_not_object_flattening()
    {
        using var actual = LegacyPayloadNormalizer.Normalize(System.Text.Encoding.UTF8.GetBytes("{\"x\":true}"),
            LegacySigningMode.SimpleV1V030, LegacySigningPurpose.Content, new Dictionary<string, string> { ["timestamp"] = "x" });
        Assert.Equal("{\"x\":true}", System.Text.Encoding.UTF8.GetString(actual.Bytes.Span));
    }

    [Fact]
    public void Unsupported_mode_payload_and_size_are_rejected()
    {
        Assert.Throws<LegacySigningInputException>(() => LegacyPayloadNormalizer.Normalize("{}"u8,
            (LegacySigningMode)99, LegacySigningPurpose.Content));
        Assert.Throws<LegacySigningInputException>(() => LegacyPayloadNormalizer.Normalize("[]"u8,
            LegacySigningMode.ObjectV030, LegacySigningPurpose.Content));
        Assert.Throws<LegacySigningInputException>(() => LegacyPayloadNormalizer.Normalize(new byte[8 * 1024 * 1024 + 1],
            LegacySigningMode.SimpleV1V030, LegacySigningPurpose.Content));
    }

    private static SensitiveBytes Normalize(string json, LegacySigningPurpose purpose = LegacySigningPurpose.Content,
        IReadOnlyDictionary<string, string>? headers = null, IReadOnlyDictionary<string, string>? custom = null) =>
        LegacyPayloadNormalizer.Normalize(System.Text.Encoding.UTF8.GetBytes(json), LegacySigningMode.ObjectV030, purpose, headers, custom);

    private sealed class SimpleFixture
    {
        public FixtureState State { get; init; }
        public int Amount { get; init; }
        public string Name { get; init; } = "";
    }
    private enum FixtureState { Ready = 1 }
    private sealed class PacketFixture { public string Name { get; init; } = ""; }
    private sealed class ObjectContractFixture
    {
        [Newtonsoft.Json.JsonProperty("legacy_name")]
        public string Name { get; init; } = "";
    }
    private sealed class PrecisionFixture
    {
        public decimal Amount { get; init; }
        public string DateText { get; init; } = "";
    }
}
