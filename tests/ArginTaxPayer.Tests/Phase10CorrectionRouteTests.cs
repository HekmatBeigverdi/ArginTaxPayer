using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10CorrectionRouteTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ArginTaxPayerDb"] =
                        "Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=unused;Password=unused",
                    ["Ghasedak:EnableLiveSending"] = "false"
                }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseEnvironment("Development");
    }

    [Fact]
    public void Each_correction_path_and_method_has_one_handler()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("/dispatch/") == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Method: method, Path: endpoint.RoutePattern.RawText)))
            .ToArray();

        var duplicateHandlers = endpoints.GroupBy(endpoint => endpoint)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        Assert.Empty(duplicateHandlers);
        foreach (var method in new[] { "GET", "POST", "PUT", "DELETE" })
            Assert.Single(endpoints, endpoint => endpoint.Method == method &&
                endpoint.Path!.EndsWith("/correction-draft"));
        Assert.Single(endpoints, endpoint => endpoint.Method == "POST" &&
            endpoint.Path!.EndsWith("/rejection-correction-draft"));
    }
}
