using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArginTaxPayer.Api.Health;

/// <summary>
/// Readiness depends on PostgreSQL, not on API process liveness.
/// Diagnostic details (host, user, secrets, exception) are not returned publicly.
/// </summary>
public sealed class DatabaseReadinessHealthCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var database = services.GetService<ArginTaxPayerDbContext>();
        if (database is null)
            return HealthCheckResult.Unhealthy("PostgreSQL is not configured.");

        try
        {
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL unavailable.");
        }
    }
}
