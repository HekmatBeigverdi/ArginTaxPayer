using System.Net;
using System.Net.Http.Json;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Actual ASP.NET Core Production middleware/routing/authorization with
/// real synthetic tenant users in dedicated TLS PostgreSQL, never customer DB.
/// Request set is read-only except test-session login/logout.
/// </summary>
public sealed class Phase18ProductionHttpTenantAcceptanceTests
{
    [Fact]
    public async Task Production_Http_denies_foreign_taxpayers_and_protects_identity_sessions()
    {
        if (Environment.GetEnvironmentVariable("RUN_PHASE18_PRODUCTION_HTTP_ACCEPTANCE") != "1")
            return;
        var connection = Environment.GetEnvironmentVariable("PHASE18_DISPOSABLE_DB_CONNECTION")
            ?? throw new InvalidOperationException("Missing disposable QA database connection.");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Database != "argin_phase18_acceptance" ||
            parsed.Username != "phase18_acceptance" ||
            parsed.Port != 15433 ||
            parsed.Host is not ("127.0.0.1" or "localhost") ||
            parsed.SslMode != SslMode.VerifyFull ||
            string.IsNullOrWhiteSpace(parsed.RootCertificate))
            throw new InvalidOperationException("Refused non-Phase18 database/role/port or unverifiable TLS.");

        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql(connection).Options);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var taxpayerA = await db.TaxpayerProfiles.AsNoTracking()
            .Where(p => p.DisplayName == "Phase18 isolated taxpayer A")
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new { p.Id, p.CustomerUserId })
            .FirstOrDefaultAsync();
        var taxpayerB = await db.TaxpayerProfiles.AsNoTracking()
            .Where(p => p.DisplayName == "Phase18 isolated taxpayer B")
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new { p.Id, p.CustomerUserId })
            .FirstOrDefaultAsync();
        Assert.NotNull(taxpayerA);
        Assert.NotNull(taxpayerB);
        Assert.NotEqual(taxpayerA!.CustomerUserId, taxpayerB!.CustomerUserId);
        Assert.True(taxpayerA.CustomerUserId.HasValue && taxpayerB.CustomerUserId.HasValue);
        var userA = await db.IdentityUsers.AsNoTracking()
            .Where(u => u.Id == taxpayerA.CustomerUserId)
            .Select(u => u.NormalizedEmail).SingleAsync();
        var userB = await db.IdentityUsers.AsNoTracking()
            .Where(u => u.Id == taxpayerB.CustomerUserId)
            .Select(u => u.NormalizedEmail).SingleAsync();
        Assert.NotNull(userA);
        Assert.NotNull(userB);
        const string testOnlyPassword = "Phase18-Test-Tenancy-Only-Password!";

        using var factory = new RealDatabaseProductionFactory(connection, true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions {
            AllowAutoRedirect = false
        });

        async Task<string> Login(string email)
        {
            using var response = await client.PostAsJsonAsync("/api/dev/identity/login",
                new { email, password = testOnlyPassword });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var token = json.GetProperty("accessToken").GetString();
            Assert.False(string.IsNullOrWhiteSpace(token));
            return token!;
        }
        var tokenA = await Login(userA!);
        var tokenB = await Login(userB!);

        async Task<HttpStatusCode> GetAs(string token, string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var result = await client.SendAsync(request);
            return result.StatusCode;
        }
        Assert.Equal(HttpStatusCode.OK,
            await GetAs(tokenA, $"/api/dev/taxpayers/{taxpayerA.Id}"));
        Assert.Equal(HttpStatusCode.NotFound,
            await GetAs(tokenA, $"/api/dev/taxpayers/{taxpayerB.Id}"));
        Assert.Equal(HttpStatusCode.OK,
            await GetAs(tokenB, $"/api/dev/taxpayers/{taxpayerB.Id}"));
        Assert.Equal(HttpStatusCode.NotFound,
            await GetAs(tokenB, $"/api/dev/taxpayers/{taxpayerA.Id}"));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/dev/taxpayers/{taxpayerA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            await GetAs("invalid", $"/api/dev/taxpayers/{taxpayerA.Id}"));
        Assert.Equal(HttpStatusCode.NotFound,
            await GetAs(tokenA, $"/api/dev/taxpayers/{Guid.NewGuid()}"));
        Assert.Equal(HttpStatusCode.Forbidden,
            await GetAs(tokenA, "/api/dev/identity/authorization/admin"));
        Assert.Equal(HttpStatusCode.OK,
            await GetAs(tokenA, "/api/dev/identity/authorization/me"));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/health/dev-identity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/openapi/v1.json")).StatusCode);

        // Real middleware refuses nonloopback internal API even with a valid token.
        using var remote = new RealDatabaseProductionFactory(connection, false);
        using var remoteClient = remote.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,
            await GetAsRemote(remoteClient, tokenA, $"/api/dev/taxpayers/{taxpayerA.Id}"));
        Assert.Equal(HttpStatusCode.NotFound,
            (await remoteClient.GetAsync("/api/dev/identity/authorization/me")).StatusCode);

        Console.WriteLine("Phase18 Production HTTP tenant isolation PASS (real DB and middleware).");
    }

    private static async Task<HttpStatusCode> GetAsRemote(HttpClient client, string token, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private sealed class RealDatabaseProductionFactory(string connection, bool loopback)
        : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["urls"] = "http://127.0.0.1:15090",
                    ["ArginTaxPayer:PublicBaseUrl"] = "https://tax.argin.ir",
                    ["ArginTaxPayer:EnableOperationalEndpoints"] = "true",
                    ["Identity:AllowDevelopmentRegistration"] = "false",
                    ["Identity:OtpDeliveryMode"] = "Ghasedak",
                    ["ConnectionStrings:ArginTaxPayerDb"] = connection,
                    ["Phase11:EnableReadOnlyQueueObserver"] = "false",
                    ["Ghasedak:EnableLiveSending"] = "false",
                    ["SandboxInvoiceLegacy:Enabled"] = "false",
                    ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "false",
                    ["SigningConnectionTest:Enabled"] = "false",
                    ["SigningKeyImport:Enabled"] = "false",
                    ["SigningKeyGeneration:Enabled"] = "false"
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
            app.Use(async (context, following) =>
            {
                context.Connection.RemoteIpAddress = loopback
                    ? IPAddress.Loopback : IPAddress.Parse("198.51.100.20");
                await following();
            });
            next(app);
        };
    }
}
