using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18LegacyAuthorityEndpointSelectorTests
{
    private static IConfiguration Settings(string sandbox = "https://sandboxrc.tax.gov.ir/req/api/",
        string production = "https://tp.tax.gov.ir/req/api/",
        bool productionEnabled = false, bool operational = false)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SandboxInvoiceLegacy:Enabled"] = "true",
            ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "true",
            ["SandboxInvoiceLegacy:ApiBaseUrl"] = sandbox,
            ["ProductionInvoiceLegacy:Enabled"] = productionEnabled ? "true" : "false",
            ["ProductionInvoiceLegacy:OperatorApprovedEndpoint"] = productionEnabled ? "true" : "false",
            ["ProductionInvoiceLegacy:ApiBaseUrl"] = production,
            ["ArginTaxPayer:EnableOperationalEndpoints"] = operational ? "true" : "false"
        }).Build();

    private static AuthorityOperationBinding Binding(TaxAuthorityEnvironment environment) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), environment);

    [Theory]
    [InlineData("ENQUEUE", "self-tsp/async/normal-enqueue")]
    [InlineData("GET_TOKEN", "self-tsp/sync/GET_TOKEN")]
    [InlineData("GET_SERVER_INFORMATION", "self-tsp/sync/GET_SERVER_INFORMATION")]
    [InlineData("INQUIRY_BY_REFERENCE_NUMBER", "self-tsp/sync/INQUIRY_BY_REFERENCE_NUMBER")]
    [InlineData("INQUIRY_BY_UID", "self-tsp/sync/INQUIRY_BY_UID")]
    public void Existing_sandbox_uses_pinned_endpoint_for_each_protocol_operation(
        string operation, string expectedPath)
    {
        var binding = Binding(TaxAuthorityEnvironment.Sandbox);
        Assert.True(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding, binding, TaxAuthorityEnvironment.Sandbox, Settings(), operation, out var uri));
        Assert.Equal("https://sandboxrc.tax.gov.ir/req/api/" + expectedPath, uri!.AbsoluteUri);
    }

    [Fact]
    public void Production_requires_matching_persisted_operation_memory_and_independent_operational_approval()
    {
        var binding = Binding(TaxAuthorityEnvironment.Production);
        var approved = Settings(productionEnabled: true, operational: true);
        Assert.True(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding, binding, TaxAuthorityEnvironment.Production, approved, "ENQUEUE", out var uri));
        Assert.Equal("https://tp.tax.gov.ir/req/api/self-tsp/async/normal-enqueue", uri!.AbsoluteUri);
        Assert.False(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding, binding, TaxAuthorityEnvironment.Production,
            Settings(productionEnabled:true), "ENQUEUE", out _));
        Assert.False(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding, binding, TaxAuthorityEnvironment.Production,
            Settings(operational:true), "ENQUEUE", out _));
        Assert.False(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding, binding with { Environment = TaxAuthorityEnvironment.Sandbox },
            TaxAuthorityEnvironment.Production, approved, "ENQUEUE", out _));
        Assert.False(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding, binding, TaxAuthorityEnvironment.Sandbox, approved, "ENQUEUE", out _));
    }

    [Theory]
    [InlineData("https://sandboxrc.tax.gov.ir.evil.test/req/api/")]
    [InlineData("http://sandboxrc.tax.gov.ir/req/api/")]
    [InlineData("https://sandboxrc.tax.gov.ir:444/req/api/")]
    [InlineData("https://sandboxrc.tax.gov.ir/req/api/../")]
    [InlineData("https://sandboxrc.tax.gov.ir/req/api/?q=1")]
    [InlineData("https://name:pass@sandboxrc.tax.gov.ir/req/api/")]
    [InlineData("https://tp.tax.gov.ir/req/api/")]
    public void Rejects_unpinned_sandbox_origin(string url)
    {
        Assert.False(Phase18LegacyAuthorityEndpointSelector.TryResolveConfiguredBase(
            TaxAuthorityEnvironment.Sandbox, Settings(sandbox:url), out _));
    }

    [Fact]
    public void Refuses_unclassified_unknown_path_and_cross_tenant_operation()
    {
        var binding = Binding(TaxAuthorityEnvironment.Sandbox);
        Assert.False(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding with { Environment = TaxAuthorityEnvironment.Unknown }, binding,
            TaxAuthorityEnvironment.Sandbox, Settings(), "ENQUEUE", out _));
        Assert.False(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding, binding with { CustomerUserId=Guid.NewGuid() },
            TaxAuthorityEnvironment.Sandbox, Settings(), "ENQUEUE", out _));
        Assert.False(Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint(
            binding, binding, TaxAuthorityEnvironment.Sandbox, Settings(),
            "../INQUIRY_BY_UID", out _));
    }
}
