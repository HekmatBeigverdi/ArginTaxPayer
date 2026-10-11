using System.Reflection;
using ArginTaxPayer.Application.Modules;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void ModuleCatalogContainsEveryApprovedContextExactlyOnce()
    {
        string[] expected =
        {
            "IdentityAccess",
            "Delegation",
            "TaxpayerMemory",
            "PartyCatalog",
            "FiscalRules",
            "Invoicing",
            "FiscalGateway",
            "Subscription",
            "Collections",
            "PartnerFinance",
            "ImportIntegration",
            "Reporting",
            "Administration",
        };
        Assert.Equal(expected.OrderBy(x => x), ModuleCatalog.All.Select(x => x.Name).OrderBy(x => x));
        Assert.All(ModuleCatalog.All, descriptor =>
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Ownership));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.SecurityInvariant));
        });
    }

    [Theory]
    [InlineData(typeof(global::ArginTaxPayer.Domain.AssemblyMarker), new string[0])]
    [InlineData(typeof(global::ArginTaxPayer.Application.AssemblyMarker), new[] { "ArginTaxPayer.Domain" })]
    [InlineData(typeof(global::ArginTaxPayer.Infrastructure.AssemblyMarker), new[] { "ArginTaxPayer.Application", "ArginTaxPayer.Domain" })]
    public void LayerDoesNotReferenceForbiddenProjectAssemblies(Type marker, string[] allowed)
    {
        var own = marker.Assembly.GetName().Name;
        var references = marker.Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("ArginTaxPayer.", StringComparison.Ordinal))
            .Where(n => n != own)
            .ToArray();

        Assert.All(references, name => Assert.Contains(name, allowed));
    }

    [Fact]
    public void ModuleDeclarationsRemainInApplicationAssembly()
    {
        var assembly = typeof(ModuleCatalog).Assembly;
        Assert.All(ModuleCatalog.All, module => Assert.Equal(assembly, module.GetType().Assembly));
    }
}
