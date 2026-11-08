using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Infrastructure;
using ArginTaxPayer.Domain.Invoices;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18AuthorityProtocolProfileTests
{
    [Fact]
    public void Resolves_only_the_explicitly_supported_legacy_contract()
    {
        Assert.True(AuthorityProtocolProfile.TryResolve(
            "legacy-self-tsp", "0.0.x", out var profile));
        Assert.Equal("legacy-self-tsp", profile!.Family);
        Assert.Equal("0.0.x", profile.Version);
        Assert.Equal("SELF_TSP legacy", profile.WireContract);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("legacy-self-tsp", "latest")]
    [InlineData("modern-rest", "2.0.32")]
    [InlineData("legacy-self-tsp", "2.0.32")]
    public void Unknown_or_inactive_profiles_fail_closed(string? family, string? version)
    {
        Assert.False(AuthorityProtocolProfile.TryResolve(family, version, out var profile));
        Assert.Null(profile);
    }

    [Fact]
    public void Published_metadata_records_archived_sdk_evidence_without_sensitive_values()
    {
        var profile = new AuthorityProtocolProfile();
        var metadata = profile.ToMetadata();

        Assert.Equal("legacy-self-tsp", metadata.Family);
        Assert.Equal("0.0.x", metadata.Version);
        Assert.Equal("SELF_TSP legacy", metadata.WireContract);
        Assert.Equal("source-0.0.30;package-0.0.34", metadata.SdkEvidence);
    }

    [Fact]
    public void Infrastructure_registers_the_current_legacy_provider()
    {
        using var services = new ServiceCollection()
            .AddArginTaxPayerInfrastructure()
            .BuildServiceProvider();

        var provider = services.GetRequiredService<IAuthorityInvoiceProvider>();
        Assert.Equal("legacy-self-tsp", provider.Family);
        Assert.Equal("0.0.x", provider.Version);
    }

    [Theory]
    [InlineData(TaxAuthorityEnvironment.Production)]
    [InlineData(TaxAuthorityEnvironment.Sandbox)]
    public void Current_profile_uses_legacy_route_for_both_environment_bound_authorities(
        TaxAuthorityEnvironment environment)
    {
        Assert.True(AuthorityProtocolProfile.CanUseLegacyRoute(
            new AuthorityProtocolProfile(), environment));
    }

    [Fact]
    public void Future_protocol_profile_cannot_be_sent_to_legacy_routes()
    {
        Assert.False(AuthorityProtocolProfile.CanUseLegacyRoute(
            new FutureProtocolProvider(), TaxAuthorityEnvironment.Production));
    }

    private sealed class FutureProtocolProvider : IAuthorityInvoiceProvider
    {
        public string Family => "modern-rest";
        public string Version => "2.0.32";
        public string WireContract => "certificate/JWS/JWE";
        public bool Supports(TaxAuthorityEnvironment environment) =>
            environment == TaxAuthorityEnvironment.Production;
    }
}
