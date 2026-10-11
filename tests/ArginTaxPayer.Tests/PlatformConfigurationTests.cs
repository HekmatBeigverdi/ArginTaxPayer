using ArginTaxPayer.Infrastructure;
using ArginTaxPayer.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PlatformConfigurationTests
{
    private static ServiceProvider CreateProvider(bool isProduction, string? url = null, string? label = "test")
    {
        var settings = new Dictionary<string, string?>
        {
            ["ArginTaxPayer:DeploymentLabel"] = label,
            ["ArginTaxPayer:PublicBaseUrl"] = url
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        InfrastructureConfigurationExtensions.AddArginTaxPayerConfiguration(services, configuration, isProduction);
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void DevelopmentAcceptsMissingPublicUrl()
    {
        using var provider = CreateProvider(false);
        Assert.Equal("test", provider.GetRequiredService<IOptions<PlatformOptions>>().Value.DeploymentLabel);
    }

    [Fact]
    public void ProductionRejectsMissingPublicUrl()
    {
        using var provider = CreateProvider(true);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PlatformOptions>>().Value);
    }

    [Fact]
    public void ProductionRejectsInsecureHttpUrl()
    {
        using var provider = CreateProvider(true, "http://example.com");
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PlatformOptions>>().Value);
    }

    [Theory]
    [InlineData("https://user:pass@example.com")]
    [InlineData("https://example.com/?token=value")]
    [InlineData("ftp://example.com")]
    [InlineData("not-a-url")]
    public void InvalidPublicUrlIsRejected(string value)
    {
        using var provider = CreateProvider(false, value);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PlatformOptions>>().Value);
    }

    [Fact]
    public void ProductionAcceptsValidHttpsUrl()
    {
        using var provider = CreateProvider(true, "https://taxpayer.example.com");
        Assert.Equal("https://taxpayer.example.com", provider.GetRequiredService<IOptions<PlatformOptions>>().Value.PublicBaseUrl);
    }

    [Fact]
    public void MissingServicesOrConfigurationIsRejected()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        Assert.Throws<ArgumentNullException>(() => global::ArginTaxPayer.Infrastructure.InfrastructureConfigurationExtensions.AddArginTaxPayerConfiguration(null!, config, false));
        Assert.Throws<ArgumentNullException>(() => global::ArginTaxPayer.Infrastructure.InfrastructureConfigurationExtensions.AddArginTaxPayerConfiguration(new ServiceCollection(), null!, false));
    }
}
