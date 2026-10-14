using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PartyApiContractTests
{
    private sealed class Factory(string environment, bool configureDb) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ArginTaxPayerDb"] = configureDb
                        ? "Host=127.0.0.1;Port=5432;Database=argintaxpayer_test;Username=unused;Password=unused;SSL Mode=Require"
                        : ""
                }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseEnvironment(environment);
    }

    [Fact]
    public async Task Development_without_database_does_not_publish_party_routes()
    {
        using var factory = new Factory("Development", false);
        using var client = factory.CreateClient();
        var url = $"/api/dev/customers/{Guid.NewGuid()}/parties";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Production_does_not_publish_party_routes_even_with_database_configured()
    {
        using var factory = new Factory("Production", true);
        using var client = factory.CreateClient();
        var url = $"/api/dev/customers/{Guid.NewGuid()}/parties";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Development_openapi_contains_party_contract_only_when_database_configured()
    {
        using var factory = new Factory("Development", true);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/dev/customers/{customerId}/parties", json);
        Assert.Contains("/contacts", json);
        Assert.Contains("/addresses", json);
        Assert.Contains("/duplicates", json);
    }
}
