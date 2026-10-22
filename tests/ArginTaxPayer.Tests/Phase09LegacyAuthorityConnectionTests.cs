using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09LegacyAuthorityConnectionTests
{
    [Fact]
    public void Fiscal_information_response_uses_the_authority_response_packet_type()
    {
        using var doc = JsonDocument.Parse("""
        {
          "errors": null,
          "result": {
            "packetType": "FISCAL_INFORMATION",
            "data": {
              "nameTrade": "Negin Hesab",
              "fiscalStatus": "ACTIVE"
            }
          }
        }
        """);

        Assert.True(LegacyTaxAuthorityConnection.TryGetPacketData(
            doc, LegacyTaxAuthorityConnection.FiscalInformationResponsePacketType, out var data));
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
    }

    [Theory]
    [InlineData("GET_TOKEN")]
    [InlineData("FISCAL_INFORMATION")]
    [InlineData("GET_FISCAL_INFORMATION")]
    public void Legacy_success_packet_reads_only_expected_data(string type)
    {
        using var doc = JsonDocument.Parse(
            "{\"result\":{\"packetType\":\"" + type + "\",\"data\":{\"token\":\"synthetic-token-not-real\",\"expiresIn\":120}},\"errors\":[]}");
        Assert.True(LegacyTaxAuthorityConnection.TryGetPacketData(doc, type, out var value));
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
    }

    [Fact]
    public void Fiscal_information_response_accepts_the_request_packet_type_used_by_older_authority_nodes()
    {
        using var doc = JsonDocument.Parse("""
        {
          "errors": [],
          "result": {
            "packetType": "GET_FISCAL_INFORMATION",
            "data": { "nameTrade": "A12WM7", "fiscalStatus": 0 }
          }
        }
        """);

        Assert.True(LegacyTaxAuthorityConnection.TryGetFiscalInformationData(doc, out var data));
        Assert.Equal("A12WM7", data.GetProperty("nameTrade").GetString());
    }

    [Fact]
    public void Legacy_token_response_does_not_require_optional_expiry_metadata()
    {
        using var doc = JsonDocument.Parse("""
        {
          "errors": [],
          "result": {
            "packetType": "GET_TOKEN",
            "data": { "token": "short-token" }
          }
        }
        """);

        Assert.True(LegacyTaxAuthorityConnection.TryGetPacketData(doc, "GET_TOKEN", out var data));
        Assert.Equal("short-token", data.GetProperty("token").GetString());
    }

    [Fact]
    public void Sdk_response_reader_accepts_data_when_authority_omits_response_packet_type()
    {
        using var doc = JsonDocument.Parse("""
        {
          "errors": [],
          "result": { "data": { "token": "authority-token" } }
        }
        """);

        Assert.True(LegacyTaxAuthorityConnection.TryGetSdkResponseData(doc, out var data));
        Assert.Equal("authority-token", data.GetProperty("token").GetString());
    }

    [Fact]
    public void Fiscal_information_snapshot_exposes_only_the_four_safe_authority_fields()
    {
        using var doc = JsonDocument.Parse("""
        { "nameTrade": "A12WM7", "fiscalStatus": 0,
          "economicCode": "14012072010", "nationalId": "14012072010",
          "token": "must-not-cross-boundary" }
        """);

        Assert.True(LegacyTaxAuthorityConnection.TryReadFiscalInformation(doc.RootElement, out var info));
        Assert.Equal("A12WM7", info.NameTrade);
        Assert.Equal("0", info.FiscalStatus);
        Assert.Equal("14012072010", info.EconomicCode);
        Assert.Equal("14012072010", info.NationalId);
    }

    [Theory]
    [InlineData("{\"result\":null}")]
    [InlineData("{\"result\":{\"packetType\":\"OTHER\",\"data\":{}}}")]
    [InlineData("{\"result\":{\"packetType\":\"GET_TOKEN\",\"data\":null}}")]
    [InlineData("{\"result\":{\"packetType\":9,\"data\":{}}}")]
    [InlineData("{\"errors\":[{\"detail\":\"must-not-echo\"}],\"result\":{\"packetType\":\"GET_TOKEN\",\"data\":{}}}")]
    public void Invalid_or_rejected_packets_cannot_claim_valid_token(string body)
    {
        using var doc = JsonDocument.Parse(body);
        Assert.False(LegacyTaxAuthorityConnection.TryGetPacketData(doc, "GET_TOKEN", out _));
    }

    [Fact]
    public void Legacy_authority_host_is_pinned_to_official_self_tsp_https()
    {
        Assert.Equal("https://tp.tax.gov.ir/req/api/", LegacyTaxAuthorityConnection.BaseUrl);
        Assert.StartsWith("https://", LegacyTaxAuthorityConnection.BaseUrl);
    }
}
