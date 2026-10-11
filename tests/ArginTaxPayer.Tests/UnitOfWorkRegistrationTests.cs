using ArginTaxPayer.Application.Abstractions.Persistence;
using ArginTaxPayer.Infrastructure;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class UnitOfWorkRegistrationTests
{
    private const string Connection =
        "Host=localhost;Database=argintaxpayer_test;Username=test;Password=placeholder;SSL Mode=Require";

    [Fact]
    public void ValidConnectionRegistersScopedUnitOfWorkUsingSameScopedDbContext()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:ArginTaxPayerDb"] = Connection }).Build();

        InfrastructurePersistenceExtensions.AddArginTaxPayerPersistence(
            services, configuration, isProduction: false);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IUnitOfWork) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var second = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Assert.Same(first, second);
        Assert.IsType<EfUnitOfWork>(first);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ArginTaxPayerDbContext>());
    }

    [Fact]
    public void MissingDevelopmentConnectionDoesNotRegisterUnitOfWork()
    {
        var services = new ServiceCollection();
        InfrastructurePersistenceExtensions.AddArginTaxPayerPersistence(
            services, new ConfigurationBuilder().Build(), isProduction: false);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IUnitOfWork));
    }
}
