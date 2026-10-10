using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void DomainAssemblyIsLoadable()
    {
        Assert.Equal("ArginTaxPayer.Domain", typeof(global::ArginTaxPayer.Domain.AssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void ApplicationAssemblyIsLoadable()
    {
        Assert.Equal("ArginTaxPayer.Application", typeof(global::ArginTaxPayer.Application.AssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void InfrastructureAssemblyIsLoadable()
    {
        Assert.Equal("ArginTaxPayer.Infrastructure", typeof(global::ArginTaxPayer.Infrastructure.AssemblyMarker).Assembly.GetName().Name);
    }
}
