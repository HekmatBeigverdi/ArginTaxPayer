using System.Net;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ApiHealthEndpointTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new DatabaseUnconfiguredApiFactory();

    [Fact]
    public async Task LivenessDoesNotRequireDatabase()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReadinessFailsClosedWhenDatabaseIsUnconfigured()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<ArginTaxPayerDbContext>());

        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(content);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain("Password=", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviousBootstrapEndpointIsUnchanged()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ArginTaxPayer", body.RootElement.GetProperty("application").GetString());
        Assert.Equal("bootstrap", body.RootElement.GetProperty("stage").GetString());
    }
}
