using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Opt-in real PostgreSQL test; transaction is rolled back even on success.
/// Requires migrations already applied to argintaxpayer_test.
/// </summary>
public sealed class OutboxPostgresIntegrationTests
{
    [Fact]
    public async Task EnforcesPerTaxpayerDeduplicationAndRollsBackTestRows()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Missing test PostgreSQL connection string.");

        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (!string.Equals(parsed.Database, "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing outbox integration tests outside argintaxpayer_test.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(connection).Options;
        await using var db = new ArginTaxPayerDbContext(options);

        Assert.Contains("20261011120000_AddOutboxMessages",
            await db.Database.GetAppliedMigrationsAsync());

        await using var transaction = await db.Database.BeginTransactionAsync();
        var taxpayer = Guid.NewGuid();
        var key = "step12:" + Guid.NewGuid().ToString("N");
        db.OutboxMessages.Add(Create(taxpayer, key));
        await db.SaveChangesAsync();

        db.OutboxMessages.Add(Create(taxpayer, key));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)exception.InnerException!).SqlState);

        // PostgreSQL aborts the current transaction after a constraint violation.
        // Disposal rolls back the whole transaction; no test records persist.
    }

    private static OutboxMessage Create(Guid taxpayer, string key) => new()
    {
        Id = Guid.NewGuid(),
        TaxpayerId = taxpayer,
        EventType = "test.step12",
        PayloadJson = "{}",
        DeduplicationKey = key,
        CreatedAtUtc = DateTime.UtcNow
    };
}
