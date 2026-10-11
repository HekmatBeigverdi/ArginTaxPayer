using ArginTaxPayer.Application.Abstractions.Persistence;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Explicitly opt-in: RUN_ARGINTAXPAYER_PG_INTEGRATION=1. Requires a dedicated
/// argintaxpayer_test database; never writes domain tables or touches dev/prod.
/// </summary>
public sealed class UnitOfWorkPostgresIntegrationTests
{
    [Fact]
    public async Task CommitAndRollbackWorkAgainstDedicatedTestDatabase()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Test database connection is required.");

        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (!string.Equals(parsed.Database, "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Integration test refuses non-test PostgreSQL database.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(connection, o => o.EnableRetryOnFailure(3))
            .Options;

        await using var context = new ArginTaxPayerDbContext(options);
        IUnitOfWork unitOfWork = new EfUnitOfWork(context);

        // The first transaction tests successful commit without persistent changes.
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await context.Database.ExecuteSqlRawAsync("SELECT 1", token);
        });

        // The second transaction must propagate errors instead of committing.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(token =>
            {
                throw new InvalidOperationException("intentional rollback test");
            }));
    }
}
