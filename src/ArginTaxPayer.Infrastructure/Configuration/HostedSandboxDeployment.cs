using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Configuration;

/// <summary>
/// Runs the existing Sandbox application with Production HTTP/security settings.
/// Its historical database name is permitted only inside the dedicated TLS
/// container, never through the development/shared database tunnel.
/// </summary>
public static class HostedSandboxDeployment
{
    public static bool IsAllowed(string connectionString, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ArginTaxPayer:HostedSandboxEnabled") ||
            configuration["ArginTaxPayer:PublicBaseUrl"] != "https://tax.argin.ir" ||
            configuration["SandboxInvoiceLegacy:ApiBaseUrl"] != "https://sandboxrc.tax.gov.ir/req/api/" ||
            configuration.GetValue<bool>("ProductionInvoiceLegacy:Enabled"))
            return false;

        try
        {
            var database = new NpgsqlConnectionStringBuilder(connectionString);
            return database.Host == "127.0.0.1" && database.Port == 15434 &&
                database.Database == "argintaxpayer_test" &&
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
