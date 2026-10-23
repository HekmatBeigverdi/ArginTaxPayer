using System.Net;
using ArginTaxPayer.Api.Taxpayers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningGenerationHttpTests
{
    [Theory]
    [InlineData("remote", 404)]
    [InlineData("origin", 403)]
    [InlineData("content-type", 415)]
    [InlineData("size", 413)]
    public async Task Generation_rejects_unsafe_requests_before_services(string scenario, int expected)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = scenario == "remote" ? IPAddress.Parse("192.0.2.1") : IPAddress.Loopback;
        http.Request.ContentType = scenario == "content-type" ? "text/plain" : "application/json";
        if (scenario == "origin") http.Request.Headers.Origin = "http://localhost:3000";
        if (scenario == "size") http.Request.ContentLength = SigningGenerationEndpoints.MaximumBodyBytes + 1;

        var result = await SigningGenerationEndpoints.GenerateAsync(Guid.NewGuid(), Guid.NewGuid(), http,
            null!, new ConfigurationBuilder().Build(), default);

        Assert.Equal(expected, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal("no-store", http.Response.Headers.CacheControl.ToString());
    }
}
