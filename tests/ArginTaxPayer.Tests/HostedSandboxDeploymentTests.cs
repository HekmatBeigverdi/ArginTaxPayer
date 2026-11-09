using ArginTaxPayer.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class HostedSandboxDeploymentTests
{
    private const string Connection = "Host=127.0.0.1;Port=15434;Database=argintaxpayer_prod;Username=argin_prod_app;SSL Mode=VerifyFull;Root Certificate=/etc/argintaxpayer/prod-db-ca.pem";

    private static IConfiguration Config(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["ArginTaxPayer:HostedSandboxEnabled"] = "true",
            ["ArginTaxPayer:EnableOperationalEndpoints"] = "true",
            ["ArginTaxPayer:PublicBaseUrl"] = "https://tax.argin.ir",
            ["SandboxInvoiceLegacy:Enabled"] = "true",
            ["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] = "true",
            ["SandboxInvoiceLegacy:ApiBaseUrl"] = "https://sandboxrc.tax.gov.ir/req/api/",
            ["ProductionInvoiceLegacy:Enabled"] = "true",
            ["ProductionInvoiceLegacy:OperatorApprovedEndpoint"] = "true",
            ["ProductionInvoiceLegacy:ApiBaseUrl"] = "https://tp.tax.gov.ir/req/api/"
        };
        foreach (var (key, value) in overrides) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Explicit_hosted_dual_authority_uses_the_single_dedicated_TLS_database()
        => Assert.True(HostedSandboxDeployment.IsAllowed(Connection, Config()));

    [Theory]
    [InlineData("Port=15434", "Port=15432")]
    [InlineData("Host=127.0.0.1", "Host=localhost")]
    [InlineData("Database=argintaxpayer_prod", "Database=argintaxpayer_test")]
    [InlineData("Username=argin_prod_app", "Username=argintaxpayer")]
    [InlineData("SSL Mode=VerifyFull", "SSL Mode=Require")]
    [InlineData("Root Certificate=/etc/argintaxpayer/prod-db-ca.pem", "Root Certificate=/tmp/other.pem")]
    public void Noncanonical_database_or_weaker_TLS_is_never_admitted(string from, string to)
        => Assert.False(HostedSandboxDeployment.IsAllowed(Connection.Replace(from, to), Config()));

    [Theory]
    [InlineData("ArginTaxPayer:HostedSandboxEnabled", "false")]
    [InlineData("ArginTaxPayer:PublicBaseUrl", "http://tax.argin.ir")]
    [InlineData("ArginTaxPayer:EnableOperationalEndpoints", "false")]
    [InlineData("SandboxInvoiceLegacy:Enabled", "false")]
    [InlineData("SandboxInvoiceLegacy:OperatorApprovedEndpoint", "false")]
    [InlineData("SandboxInvoiceLegacy:ApiBaseUrl", "https://tp.tax.gov.ir/req/api/")]
    [InlineData("ProductionInvoiceLegacy:Enabled", "false")]
    [InlineData("ProductionInvoiceLegacy:OperatorApprovedEndpoint", "false")]
    [InlineData("ProductionInvoiceLegacy:ApiBaseUrl", "https://sandboxrc.tax.gov.ir/req/api/")]
    public void Hosted_dual_authority_requires_two_explicit_pinned_environments(string key, string value)
        => Assert.False(HostedSandboxDeployment.IsAllowed(Connection, Config((key, value))));

    [Fact]
    public void Malformed_connection_is_rejected_without_exposing_its_contents()
        => Assert.False(HostedSandboxDeployment.IsAllowed("not a connection string", Config()));

    [Theory]
    [InlineData("argintaxpayer_test")]
    [InlineData("argintaxpayer_prod")]
    public void Sandbox_data_store_names_are_supported_for_local_and_hosted_modes(string database)
        => Assert.True(HostedSandboxDeployment.IsSandboxStoreDatabaseName(database));

    [Fact]
    public void Unknown_database_is_not_a_sandbox_data_store()
        => Assert.False(HostedSandboxDeployment.IsSandboxStoreDatabaseName("argintaxpayer_dev"));
}
