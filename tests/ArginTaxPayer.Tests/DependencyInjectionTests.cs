using ArginTaxPayer.Application;
using ArginTaxPayer.Application.Modules;
using ArginTaxPayer.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using InfrastructureAssemblyMarker = global::ArginTaxPayer.Infrastructure.AssemblyMarker;

namespace ArginTaxPayer.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void BothRegistrationsResolveAllThirteenModules()
    {
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();
        services.AddArginTaxPayerInfrastructure();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var modules = provider.GetServices<IModuleDescriptor>().ToArray();

        Assert.Equal(13, modules.Length);
        Assert.Equal(
            ModuleCatalog.All.Select(x => x.Name).OrderBy(x => x),
            modules.Select(x => x.Name).OrderBy(x => x));
        Assert.NotNull(provider.GetRequiredService<InfrastructureAssemblyMarker>());
    }

    [Fact]
    public void RegistrationsAreIdempotent()
    {
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();
        services.AddArginTaxPayerInfrastructure();
        services.AddArginTaxPayerApplication();
        services.AddArginTaxPayerInfrastructure();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        Assert.Equal(13, provider.GetServices<IModuleDescriptor>().Count());
        Assert.Single(services, service => service.ServiceType == typeof(InfrastructureAssemblyMarker));
    }

    [Fact]
    public void ApplicationCanBeComposedWithoutInfrastructure()
    {
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        Assert.Equal(13, provider.GetServices<IModuleDescriptor>().Count());
        Assert.Null(provider.GetService<InfrastructureAssemblyMarker>());
    }

    [Fact]
    public void NullServiceCollectionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => DependencyInjectionApplication(null!));
        Assert.Throws<ArgumentNullException>(() => DependencyInjectionInfrastructure(null!));
    }

    private static void DependencyInjectionApplication(IServiceCollection services) =>
        ArginTaxPayer.Application.DependencyInjection.AddArginTaxPayerApplication(services);

    private static void DependencyInjectionInfrastructure(IServiceCollection services) =>
        ArginTaxPayer.Infrastructure.DependencyInjection.AddArginTaxPayerInfrastructure(services);
}
