using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Phase18 Production integration admission tests. Uses a deliberately unreachable
/// PostgreSQL address; never connects to or modifies any customer/test DB.
/// </summary>
public sealed class Phase18ProductionIsolationTests
{
    private const string Route = "/api/dev/identity/register";

    [Fact]
    public async Task Production_enabled_internal_API_is_not_reachable_from_non_loopback()
    {
        using var factory = new ProductionFactory(false);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/dev/identity/authorization/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/dev/taxpayers/00000000-0000-0000-0000-000000000000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/health/dev-identity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Production_loopback_does_not_allow_development_registration()
    {
        using var factory = new ProductionFactory(true);
        using var client = factory.CreateClient();

        // Registration should reject before a DB query or identity persistence.
        using var response = await client.PostAsJsonAsync(Route,
            new { email = "no-registration@example.invalid", password = "NeverRegister123!" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/health/dev-identity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/health/live")).StatusCode);
    }

    private sealed class ProductionFactory(bool loopback) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["urls"] = "http://127.0.0.1:15090",
                    ["ArginTaxPayer:DeploymentLabel"] = "production",
                    ["ArginTaxPayer:PublicBaseUrl"] = "https://tax.argin.ir",
                    ["ArginTaxPayer:EnableOperationalEndpoints"] = "true",
                    ["Identity:AllowDevelopmentRegistration"] = "false",
                    ["Identity:OtpDeliveryMode"] = "Ghasedak",
                    // Port is intentionally NEVER listened on, no DB is created.
                    // SSL must be required by production persistence registration.
                    ["ConnectionStrings:ArginTaxPayerDb"] =
                        "Host=127.0.0.1;Port=15992;Database=phase18_unreachable;Username=phase18_nodb;Password=not-a-credential;SSL Mode=Require;Timeout=1;Command Timeout=1",
                    ["Phase11:EnableReadOnlyQueueObserver"] = "false",
                    ["Ghasedak:EnableLiveSending"] = "false",
                    ["SandboxInvoiceLegacy:Enabled"] = "false",
                    ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "false"
                }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new CallerFilter(loopback)));
        }
    }

    private sealed class CallerFilter(bool loopback) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.Connection.RemoteIpAddress = loopback
                    ? IPAddress.Loopback : IPAddress.Parse("198.51.100.20");
                await continuation();
            });
            next(app);
        };
    }
}
