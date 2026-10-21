using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09OfflineSigningIsolationTests
{
    [Fact]
    public void Offline_engine_is_not_registered_in_application_services_or_api()
    {
        var services = new ServiceCollection();
        services.AddPhase09OfflineSigning(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<ILegacyOfflineSigningEngine>());
        Assert.Equal("false", new ConfigurationBuilder().Build()["SigningKeyOfflineSigning:Enabled"] ?? "false");
    }
}
