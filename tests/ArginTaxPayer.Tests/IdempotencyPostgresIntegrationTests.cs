using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Opt-in uniqueness test on argintaxpayer_test. Test transaction is rolled back.
/// </summary>
public sealed class IdempotencyPostgresIntegrationTests
{
    [Fact]
    public async Task DuplicateOperationKeyForSameTaxpayerIsRejected()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Test database connection is missing.");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (!string.Equals(parsed.Database, "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Integration test requires argintaxpayer_test.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(connection).Options;
        await using var db = new ArginTaxPayerDbContext(options);
        Assert.Contains("20261011180000_AddIdempotencyReservations",
            await db.Database.GetAppliedMigrationsAsync());

        await using var transaction = await db.Database.BeginTransactionAsync();
        var taxpayer = Guid.NewGuid();
        var key = "step13-" + Guid.NewGuid().ToString("N");

        db.IdempotencyReservations.Add(Record(taxpayer, key));
        await db.SaveChangesAsync();

        db.IdempotencyReservations.Add(Record(taxpayer, key));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        // Transaction is aborted after constraint violation and rolled back on disposal.
    }

    private static IdempotencyReservation Record(Guid taxpayerId, string key) => new()
    {
        Id = Guid.NewGuid(),
        TaxpayerId = taxpayerId,
        Operation = "invoice.submit",
        Key = key,
        RequestFingerprint = new string('a', 64),
        CreatedAtUtc = DateTime.UtcNow
    };
}
