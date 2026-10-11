using System.Net;
using ArginTaxPayer.Api.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ApiCorrelationTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ArginTaxPayerDb"] = ""
                }));
        });

    [Fact]
    public async Task ValidCorrelationIdIsPreservedAcrossBootstrap()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "trace_ABC-123");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("trace_ABC-123",
            Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
    }

    [Theory]
    [InlineData("contains space")]
    [InlineData("bad?secret=password")]
    [InlineData("abc/def")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task InvalidCorrelationIdIsReplaced(string submittedId)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, submittedId);

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var received = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.NotEqual(submittedId, received);
        Assert.True(CorrelationIdMiddleware.IsValid(received));
    }

    [Fact]
    public async Task MissingCorrelationIdIsGeneratedEvenForUnhealthyReadiness()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready?private_token=do-not-echo");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.True(CorrelationIdMiddleware.IsValid(correlationId));
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private_token", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatorRejectsControlCharactersAndEmptyValues()
    {
        Assert.False(CorrelationIdMiddleware.IsValid(null));
        Assert.False(CorrelationIdMiddleware.IsValid(""));
        Assert.False(CorrelationIdMiddleware.IsValid("line\nbreak"));
        Assert.True(CorrelationIdMiddleware.IsValid("A_1-z"));
    }
}
