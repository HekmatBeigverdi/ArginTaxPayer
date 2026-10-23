using System.Net;
using System.Security.Cryptography;
using ArginTaxPayer.Api.Taxpayers;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Import;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09ImportOnboardingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Matching_public_key_is_checked_before_custody(bool rsaPublicFormat)
    {
        using var rsa = RSA.Create(2048);
        var journal = new Phase09SigningImportWorkflowTests.Journal();
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        var service = new SigningKeyImportService(journal, custody, new());
        var request = Request(rsaPublicFormat ? rsa.ExportRSAPublicKeyPem() : rsa.ExportSubjectPublicKeyInfoPem());
        var result = await service.ImportAsync("test", request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Pem));
        Assert.Equal(SigningGenerationStatus.Created, result.Status);
        Assert.Equal(1, custody.Stages);
        Assert.False(result.PublicKey!.PortalUploadApproved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mismatch_or_malformed_public_key_never_reserves_or_stores(bool malformed)
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var journal = new Phase09SigningImportWorkflowTests.Journal();
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        var service = new SigningKeyImportService(journal, custody, new());
        var input = Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Pem);
        var result = await service.ImportAsync("test", Request(malformed ? "invalid" : other.ExportSubjectPublicKeyInfoPem()), input);
        Assert.Equal(SigningGenerationStatus.Invalid, result.Status);
        Assert.Equal(0, journal.Reservations);
        Assert.Equal(0, custody.Stages);
        Assert.Throws<ObjectDisposedException>(() => input.KeyBytes);
    }

    [Theory]
    [InlineData("remote", 404)]
    [InlineData("origin", 403)]
    [InlineData("content-type", 415)]
    [InlineData("size", 413)]
    public async Task Http_rejects_unsafe_requests_before_services_or_body(string scenario, int expected)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = scenario == "remote" ? IPAddress.Parse("192.0.2.1") : IPAddress.Loopback;
        http.Request.ContentType = scenario == "content-type" ? "text/plain" : "application/json";
        if (scenario == "origin") http.Request.Headers.Origin = "http://localhost:3000";
        if (scenario == "size") http.Request.ContentLength = SigningImportEndpoints.MaximumBodyBytes + 1;
        var result = await SigningImportEndpoints.ImportAsync(Guid.NewGuid(), Guid.NewGuid(), http,
            null!, null!, new ConfigurationBuilder().Build(), default);
        Assert.Equal(expected, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal("no-store", http.Response.Headers.CacheControl.ToString());
    }

    private static ImportSigningKeyRequest Request(string publicPem) => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), SigningOperationPolicy.LegacyProfile, SigningOperationPolicy.ConsentVersion, true, publicPem);
}
