using ArginTaxPayer.Application;
using ArginTaxPayer.Application.Abstractions.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class TaxpayerScopeTests
{
    [Fact]
    public void UninitializedScopeFailsClosed()
    {
        using var provider = Configure();
        using var scope = provider.CreateScope();
        var taxpayerScope = scope.ServiceProvider.GetRequiredService<ITaxpayerScope>();
        Assert.False(taxpayerScope.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => taxpayerScope.RequireTaxpayerId());
    }

    [Fact]
    public void InitializationIsSingleShotAndRejectsEmptyIdentifiers()
    {
        using var provider = Configure();
        using var scope = provider.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<ITaxpayerScopeInitializer>();
        var reader = scope.ServiceProvider.GetRequiredService<ITaxpayerScope>();
        var taxpayer = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => initializer.Initialize(Guid.Empty));
        initializer.Initialize(taxpayer);
        Assert.True(reader.IsInitialized);
        Assert.Equal(taxpayer, reader.RequireTaxpayerId());
        Assert.Throws<InvalidOperationException>(() => initializer.Initialize(taxpayer));
        Assert.Throws<InvalidOperationException>(() => initializer.Initialize(Guid.NewGuid()));
    }

    [Fact]
    public void TwoDependencyInjectionScopesCannotSeeEachOthersTaxpayer()
    {
        using var provider = Configure();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = Guid.NewGuid();
        firstScope.ServiceProvider.GetRequiredService<ITaxpayerScopeInitializer>().Initialize(first);
        var firstReader = firstScope.ServiceProvider.GetRequiredService<ITaxpayerScope>();
        var secondReader = secondScope.ServiceProvider.GetRequiredService<ITaxpayerScope>();

        Assert.Equal(first, firstReader.RequireTaxpayerId());
        Assert.False(secondReader.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => secondReader.RequireTaxpayerId());

        var second = Guid.NewGuid();
        secondScope.ServiceProvider.GetRequiredService<ITaxpayerScopeInitializer>().Initialize(second);
        Assert.Equal(second, secondReader.RequireTaxpayerId());
        Assert.Equal(first, firstReader.RequireTaxpayerId());
        Assert.NotSame(firstReader, secondReader);
    }

    private static ServiceProvider Configure()
    {
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
