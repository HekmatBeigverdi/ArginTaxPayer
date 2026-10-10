using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void DomainAssemblyIsLoadable()
    {
        Assert.Equal("ArginTaxPayer.Domain", typeof(Domain.AssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void ApplicationAssemblyIsLoadable()
    {
        Assert.Equal("ArginTaxPayer.Application", typeof(Application.AssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void InfrastructureAssemblyIsLoadable()
    {
        Assert.Equal("ArginTaxPayer.Infrastructure", typeof(Infrastructure.AssemblyMarker).Assembly.GetName().Name);
    }
}
