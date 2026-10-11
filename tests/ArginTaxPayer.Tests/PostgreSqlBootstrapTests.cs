using ArginTaxPayer.Infrastructure;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PostgreSqlBootstrapTests
{
    private static IConfiguration Configuration(string? connectionString) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ArginTaxPayerDb"] = connectionString
        }).Build();

    private const string Valid = "Host=localhost;Port=5432;Database=argintaxpayer_test;Username=argintaxpayer;Password=not-a-real-secret;SSL Mode=Require";

    [Fact]
    public void DevelopmentWithoutDatabasePreservesBootstrapBehavior()
    {
        var services = new ServiceCollection();
        services.AddArginTaxPayerPersistence(Configuration(null), false);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(ArginTaxPayerDbContext));
    }

    [Fact]
    public void ProductionWithoutDatabaseFailsClosed()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(
            () => services.AddArginTaxPayerPersistence(Configuration(null), true));
    }

    [Fact]
    public void ValidConnectionRegistersScopedNpgsqlContextWithoutConnecting()
    {
        var services = new ServiceCollection();
        services.AddArginTaxPayerPersistence(Configuration(Valid), true);
        Assert.Contains(services, x => x.ServiceType == typeof(ArginTaxPayerDbContext) &&
                                        x.Lifetime == ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArginTaxPayerDbContext>();
        Assert.True(db.Database.IsNpgsql());
        Assert.Empty(db.Model.GetEntityTypes());
    }

    [Fact]
    public void ProductionRejectsNonTlsPostgreSqlConnection()
    {
        var services = new ServiceCollection();
        var nonTls = "Host=localhost;Database=argintaxpayer;Username=test;SSL Mode=Disable";
        Assert.Throws<InvalidOperationException>(
            () => services.AddArginTaxPayerPersistence(Configuration(nonTls), true));
    }

    [Fact]
    public void MissingIdentityFieldsAreRejected()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(
            () => services.AddArginTaxPayerPersistence(
                Configuration("Host=localhost;Database=argintaxpayer;SSL Mode=Require"), true));
    }
}
