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
            ["ArginTaxPayer:PublicBaseUrl"] = "https://tax.argin.ir",
            ["SandboxInvoiceLegacy:ApiBaseUrl"] = "https://sandboxrc.tax.gov.ir/req/api/",
            ["ProductionInvoiceLegacy:Enabled"] = "true"
        };
        foreach (var (key, value) in overrides) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Hosted_dual_authority_accepts_the_dedicated_TLS_production_database()
        => Assert.True(HostedSandboxDeployment.IsAllowed(Connection, Config()));

    [Theory]
    [InlineData("Port=15434", "Port=15432")]
    [InlineData("Host=127.0.0.1", "Host=localhost")]
    [InlineData("Database=argintaxpayer_prod", "Database=argintaxpayer_test")]
    [InlineData("Database=argintaxpayer_prod", "Database=argintaxpayer_dev")]
    [InlineData("Username=argin_prod_app", "Username=argintaxpayer")]
    [InlineData("SSL Mode=VerifyFull", "SSL Mode=Require")]
    [InlineData("Root Certificate=/etc/argintaxpayer/prod-db-ca.pem", "Root Certificate=/tmp/other.pem")]
    public void Shared_local_database_or_weaker_TLS_is_never_admitted(string from, string to)
        => Assert.False(HostedSandboxDeployment.IsAllowed(Connection.Replace(from, to), Config()));

    [Theory]
    [InlineData("ArginTaxPayer:HostedSandboxEnabled", "false")]
    [InlineData("ArginTaxPayer:PublicBaseUrl", "http://tax.argin.ir")]
    [InlineData("SandboxInvoiceLegacy:ApiBaseUrl", "https://tp.tax.gov.ir/req/api/")]
    [InlineData("ProductionInvoiceLegacy:Enabled", "false")]
    public void Both_authorities_and_the_production_host_must_be_explicitly_enabled(string key, string value)
        => Assert.False(HostedSandboxDeployment.IsAllowed(Connection, Config((key, value))));

    [Fact]
    public void Malformed_connection_is_rejected_without_exposing_its_contents()
        => Assert.False(HostedSandboxDeployment.IsAllowed("not a connection string", Config()));
    [Fact]
    public void Legacy_sandbox_database_admission_supports_both_approved_deployments()
    {
        using var connection = new Npgsql.NpgsqlConnection(Connection);
        Assert.True(SandboxDatabasePolicy.IsAllowed("argintaxpayer_test", connection, null));
        Assert.True(SandboxDatabasePolicy.IsAllowed("argintaxpayer_prod", connection, Config()));
        Assert.False(SandboxDatabasePolicy.IsAllowed("argintaxpayer_prod", connection, null));
        Assert.False(SandboxDatabasePolicy.IsAllowed("unknown", connection, Config()));
        Assert.False(SandboxDatabasePolicy.IsAllowed(null, connection, Config()));
        Assert.False(SandboxDatabasePolicy.IsAllowed("argintaxpayer_prod", connection,
            Config(("SandboxInvoiceLegacy:ApiBaseUrl", "https://tp.tax.gov.ir/req/api/"))));
        using var shared = new Npgsql.NpgsqlConnection(Connection.Replace("Port=15434", "Port=15432"));
        Assert.False(SandboxDatabasePolicy.IsAllowed("argintaxpayer_prod", shared, Config()));
    }
}
