using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09LegacyAuthorityConnectionTests
{
    [Theory]
    [InlineData("GET_TOKEN")]
    [InlineData("GET_FISCAL_INFORMATION")]
    public void Legacy_success_packet_reads_only_expected_data(string type)
    {
        using var doc = JsonDocument.Parse(
            "{\"result\":{\"packetType\":\"" + type + "\",\"data\":{\"token\":\"synthetic-token-not-real\",\"expiresIn\":120}},\"errors\":[]}");
        Assert.True(LegacyTaxAuthorityConnection.TryGetPacketData(doc, type, out var value));
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
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
