using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Configuration;

/// <summary>
/// Allows the hosted application to use Sandbox and Production fiscal memories
/// against the dedicated TLS production database. Dispatch authority remains
/// selected by each fiscal memory's persisted authority environment.
/// </summary>
public static class HostedSandboxDeployment
{
    public static bool IsAllowed(string connectionString, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ArginTaxPayer:HostedSandboxEnabled") ||
            configuration["ArginTaxPayer:PublicBaseUrl"] != "https://tax.argin.ir" ||
            configuration["SandboxInvoiceLegacy:ApiBaseUrl"] != "https://sandboxrc.tax.gov.ir/req/api/" ||
            !configuration.GetValue<bool>("ProductionInvoiceLegacy:Enabled"))
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
