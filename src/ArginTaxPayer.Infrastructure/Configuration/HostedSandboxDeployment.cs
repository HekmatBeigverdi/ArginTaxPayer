using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Configuration;

/// <summary>
/// Runs both memory-selected authority environments with Production HTTP/security
/// settings. Sandbox tables share the isolated Production application database;
/// the persisted fiscal-memory environment selects the pinned endpoint.
/// </summary>
public static class HostedSandboxDeployment
{
    public static bool IsSandboxStoreDatabaseName(string? databaseName) =>
        databaseName is "argintaxpayer_test" or "argintaxpayer_prod";

    public static bool IsAllowed(string connectionString, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ArginTaxPayer:HostedSandboxEnabled") ||
            !configuration.GetValue<bool>("ArginTaxPayer:EnableOperationalEndpoints") ||
            configuration["ArginTaxPayer:PublicBaseUrl"] != "https://tax.argin.ir" ||
            !configuration.GetValue<bool>("SandboxInvoiceLegacy:Enabled") ||
            !configuration.GetValue<bool>("SandboxInvoiceLegacy:OperatorApprovedEndpoint") ||
            configuration["SandboxInvoiceLegacy:ApiBaseUrl"] != "https://sandboxrc.tax.gov.ir/req/api/" ||
            !configuration.GetValue<bool>("ProductionInvoiceLegacy:Enabled") ||
            !configuration.GetValue<bool>("ProductionInvoiceLegacy:OperatorApprovedEndpoint") ||
            configuration["ProductionInvoiceLegacy:ApiBaseUrl"] != "https://tp.tax.gov.ir/req/api/")
            return false;

        try
        {
            var database = new NpgsqlConnectionStringBuilder(connectionString);
            return database.Host == "127.0.0.1" && database.Port == 15434 &&
                database.Database == "argintaxpayer_prod" &&
                database.Username == "argin_prod_app" &&
                database.SslMode == SslMode.VerifyFull &&
                database.RootCertificate == "/etc/argintaxpayer/prod-db-ca.pem";
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
