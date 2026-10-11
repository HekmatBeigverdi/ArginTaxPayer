using ArginTaxPayer.Application.Abstractions.Security;
using ArginTaxPayer.Infrastructure;
using ArginTaxPayer.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class SecretProviderTests
{
    [Fact]
    public void OnlyPrefixedEnvironmentVariablesAreRead()
    {
        var accessed = new List<string>();
        ISecretProvider provider = new EnvironmentSecretProvider(name =>
        {
            accessed.Add(name);
            return name == "ARGIN_SECRET_TEST_KEY" ? "demo-placeholder-value" : null;
        });

        var secret = provider.GetRequired("TEST_KEY");

        Assert.Equal("demo-placeholder-value", secret.Reveal());
        Assert.Equal("[REDACTED]", secret.ToString());
        Assert.Equal(new[] { "ARGIN_SECRET_TEST_KEY" }, accessed);
    }

    [Fact]
    public void MissingRequiredSecretDoesNotDiscloseItsNameOrValue()
    {
        ISecretProvider provider = new EnvironmentSecretProvider(_ => null);
        Assert.Null(provider.GetOptional("MISSING"));
        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequired("MISSING"));
        Assert.DoesNotContain("MISSING", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("db/password")]
    [InlineData("private_key")]
    [InlineData("NAME\nFORGED")]
    public void RejectsMalformedNames(string name)
    {
        Assert.Throws<ArgumentException>(() => EnvironmentSecretProvider.EnvironmentVariableName(name));
    }

    [Fact]
    public void DependencyInjectionRegistersTheProviderOnce()
    {
        var services = new ServiceCollection();
        services.AddArginTaxPayerInfrastructure();
        services.AddArginTaxPayerInfrastructure();

        Assert.Single(services.Where(d => d.ServiceType == typeof(ISecretProvider)));
        using var provider = services.BuildServiceProvider();
        Assert.IsType<EnvironmentSecretProvider>(provider.GetRequiredService<ISecretProvider>());
    }
}
