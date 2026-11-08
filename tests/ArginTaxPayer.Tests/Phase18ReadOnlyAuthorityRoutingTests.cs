using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18ReadOnlyAuthorityRoutingTests
{
    [Theory]
    [InlineData(null, 1, 1, false, 1)]
    [InlineData(null, 2, 1, false, 2)]
    [InlineData(null, 1, 1, true, null)]
    [InlineData(1, null, 2, false, 1)]
    [InlineData(2, null, 2, false, 2)]
    [InlineData(1, 2, 2, false, null)]
    [InlineData(null, 1, 2, false, null)]
    public void Temporary_environment_only_routes_read_only_tests_for_unclassified_unattempted_memories(
        int? storedEnvironment, int? requestedEnvironment, int status, bool hasDispatchHistory,
        int? expected)
    {
        var selected = LegacyTaxAuthorityConnection.SelectReadOnlyTestEnvironment(
            storedEnvironment is null ? null : (FiscalConnectionEnvironment)storedEnvironment,
            requestedEnvironment is null ? null : (FiscalConnectionEnvironment)requestedEnvironment,
            (FiscalMemoryStatus)status, hasDispatchHistory);
        Assert.Equal(expected is null ? null : (FiscalConnectionEnvironment?)expected, selected);
    }

    [Theory]
    [InlineData(FiscalConnectionEnvironment.Production, "tp.tax.gov.ir")]
    [InlineData(FiscalConnectionEnvironment.Sandbox, "sandboxrc.tax.gov.ir")]
    public async Task Read_only_probe_uses_one_environment_for_token_and_fiscal_information(
        FiscalConnectionEnvironment environment, string host)
    {
        using var rsa = RSA.Create(2048);
        using var secret = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var requests = new List<(Uri Uri, string? Authorization, string PacketType, string FiscalId)>();
        using var http = new HttpClient(new Handler(request => {
            using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            var packet = body.RootElement.GetProperty("packet");
            requests.Add((request.RequestUri!, request.Headers.Authorization?.ToString(),
                packet.GetProperty("packetType").GetString()!, packet.GetProperty("fiscalId").GetString()!));
            var responseBody = requests.Count switch
            {
                1 => """{"errors":[],"result":{"data":{"publicKeys":[]}}}""",
                2 => """{"errors":[],"result":{"data":{"token":"synthetic-token"}}}""",
                _ => """{"errors":[],"result":{"data":{"nameTrade":"Synthetic","fiscalStatus":"ACTIVE","economicCode":"14012072010","nationalId":"14012072010"}}}"""
            };
            return new(HttpStatusCode.OK) { Content = new StringContent(responseBody) };
        }));
        using var connection = new LegacyTaxAuthorityConnection(null!, null!, null!, null!, http,
            NullLogger<LegacyTaxAuthorityConnection>.Instance);

        var result = await connection.ProbeFiscalInformationAsync(environment, "SAME01", secret,
            rsa.ExportSubjectPublicKeyInfo(), default);

        Assert.NotNull(result);
        Assert.Equal(3, requests.Count);
        Assert.All(requests, request => {
            Assert.Equal("https", request.Uri.Scheme);
            Assert.Equal(host, request.Uri.Host);
            Assert.Equal("SAME01", request.FiscalId);
        });
        Assert.Equal("/req/api/self-tsp/sync/GET_SERVER_INFORMATION", requests[0].Uri.AbsolutePath);
        Assert.Equal("/req/api/self-tsp/sync/GET_TOKEN", requests[1].Uri.AbsolutePath);
        Assert.Equal("/req/api/self-tsp/sync/GET_FISCAL_INFORMATION", requests[2].Uri.AbsolutePath);
        Assert.Null(requests[0].Authorization);
        Assert.Null(requests[1].Authorization);
        Assert.Equal("Bearer synthetic-token", requests[2].Authorization);
        Assert.Equal("GET_SERVER_INFORMATION", requests[0].PacketType);
        Assert.Equal("GET_TOKEN", requests[1].PacketType);
        Assert.Equal("GET_FISCAL_INFORMATION", requests[2].PacketType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Unclassified_or_unknown_memory_environment_makes_no_http_request(int? value)
    {
        var requests = 0;
        using var http = new HttpClient(new Handler(_ => { requests++; return new(HttpStatusCode.OK); }));
        using var connection = new LegacyTaxAuthorityConnection(null!, null!, null!, null!, http,
            NullLogger<LegacyTaxAuthorityConnection>.Instance);
        using var secret = SensitiveBytes.TakeOwnership([]);

        var result = await connection.ProbeFiscalInformationAsync(
            value is null ? null : (FiscalConnectionEnvironment)value, "SAME01", secret, [], default);

        Assert.Null(result);
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task A_key_mismatch_is_rejected_before_any_authority_request()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        using var secret = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var requests = 0;
        using var http = new HttpClient(new Handler(_ => { requests++; return new(HttpStatusCode.OK); }));
        using var connection = new LegacyTaxAuthorityConnection(null!, null!, null!, null!, http,
            NullLogger<LegacyTaxAuthorityConnection>.Instance);

        await Assert.ThrowsAsync<KeyProtectionException>(() => connection.ProbeFiscalInformationAsync(
            FiscalConnectionEnvironment.Production, "SAME01", secret, other.ExportSubjectPublicKeyInfo(), default));

        Assert.Equal(0, requests);
    }

    [Theory]
    [InlineData(FiscalConnectionEnvironment.Production, "tp.tax.gov.ir")]
    [InlineData(FiscalConnectionEnvironment.Sandbox, "sandboxrc.tax.gov.ir")]
    public async Task Unavailable_authority_never_falls_back_to_the_other_environment(
        FiscalConnectionEnvironment environment, string expectedHost)
    {
        using var rsa = RSA.Create(2048);
        using var secret = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var hosts = new List<string>();
        using var http = new HttpClient(new Handler(request => {
            hosts.Add(request.RequestUri!.Host);
            throw new HttpRequestException("Synthetic offline failure");
        }));
        using var connection = new LegacyTaxAuthorityConnection(null!, null!, null!, null!, http,
            NullLogger<LegacyTaxAuthorityConnection>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => connection.ProbeFiscalInformationAsync(
            environment, "SAME01", secret, rsa.ExportSubjectPublicKeyInfo(), default));

        Assert.Equal(new[] { expectedHost }, hosts);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }
}
