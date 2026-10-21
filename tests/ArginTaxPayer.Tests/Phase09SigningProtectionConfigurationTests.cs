using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningProtectionConfigurationTests
{
    [Fact]
    public async Task Missing_configuration_registers_fail_closed_provider_and_store()
    {
        var services = new ServiceCollection();
        services.AddPhase09KeyProtection(new ConfigurationBuilder().Build());
        using var container = services.BuildServiceProvider();
        var id = Phase09SigningKeyProtectionTests.Identity();
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() =>
            container.GetRequiredService<ProtectedKeyEnvelopeStore>().StageAsync(id, new byte[] { 1 }));
        Assert.Equal(KeyProtectionFailure.Unavailable, error.Failure);
        Assert.IsType<UnavailableKeyProtectionProvider>(container.GetRequiredService<IKeyProtectionProvider>());
    }

    [Fact]
    public void Enabled_without_complete_configuration_is_rejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["SigningKeyProtection:Enabled"] = "true" }).Build();
        Assert.Throws<KeyProtectionException>(() => new ServiceCollection().AddPhase09KeyProtection(configuration));
    }

    [Fact]
    public async Task Token_source_rejects_missing_file_without_disclosing_path()
    {
        var path = Path.Combine(Path.GetTempPath(), "missing-token-canary-" + Guid.NewGuid().ToString("N"));
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() =>
            new VaultAgentTokenSource(path).ReadAsync(default).AsTask());
        Assert.DoesNotContain(path, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task Agent_token_is_reread_after_rotation()
    {
        var path = Path.Combine(Path.GetTempPath(), "argin-synthetic-token-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllTextAsync(path, "synthetic-one");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var source = new VaultAgentTokenSource(path);
            Assert.Equal("synthetic-one", await source.ReadAsync(default));
            await File.WriteAllTextAsync(path, "synthetic-two");
            Assert.Equal("synthetic-two", await source.ReadAsync(default));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.GroupRead);
                await Assert.ThrowsAsync<KeyProtectionException>(() => source.ReadAsync(default).AsTask());
            }
        }
        finally { File.Delete(path); }
    }
}
