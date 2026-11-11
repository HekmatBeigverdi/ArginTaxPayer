using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Infrastructure.Configuration;

/// <summary>Database deployment admission only; never selects an authority, memory or key.</summary>
public static class SandboxDatabasePolicy
{
    public static bool IsAllowed(string? databaseName, DbConnection connection, IConfiguration? configuration) =>
        databaseName == "argintaxpayer_test" ||
        databaseName == "argintaxpayer_prod" && configuration is not null &&
        HostedSandboxDeployment.IsAllowed(connection.ConnectionString, configuration);
}
