using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ApiOpenApiTests
{
    private static WebApplicationFactory<Program> Factory(string environment) =>
        new ApiFactory(environment);

    private sealed class ApiFactory(string environment) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ArginTaxPayerDb"] =
                        environment == "Production"
                            ? "Host=127.0.0.1;Port=5432;Database=argintaxpayer_test;Username=test;Password=test;SSL Mode=Require"
                            : "",
                    ["ArginTaxPayer:PublicBaseUrl"] = "https://example.test"
                }));

            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseEnvironment(environment);
    }

    [Fact]
    public async Task DevelopmentOpenApiExposesPlatformContract()
    {
        using var factory = Factory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.StartsWith("3.", json.RootElement.GetProperty("openapi").GetString());
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/", out _));
        Assert.True(paths.TryGetProperty("/health/live", out _));
        Assert.True(paths.TryGetProperty("/health/ready", out _));
    }

    [Fact]
    public async Task ProductionDoesNotExposeOpenApi()
    {
        using var factory = Factory("Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentKeepsBootstrapAndLivenessWorking()
    {
        using var factory = Factory("Development");
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
}
