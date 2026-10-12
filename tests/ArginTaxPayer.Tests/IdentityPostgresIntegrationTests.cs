using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Opt-in PostgreSQL verification against the dedicated test database only.</summary>
public sealed class IdentityPostgresIntegrationTests
{
    [Fact]
    public async Task AppliedIdentityMigrationCreatesTablesAndEnforcesUniqueEmail()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Missing explicit test database connection.");
        var cs = new NpgsqlConnectionStringBuilder(connection);
        if (!string.Equals(cs.Database, "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Identity integration requires argintaxpayer_test.");

        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(connection).Options);

        Assert.Contains("20261012010000_AddIdentityFoundation",
            await db.Database.GetAppliedMigrationsAsync());

        await using var transaction = await db.Database.BeginTransactionAsync();
        var email = "phase03-" + Guid.NewGuid().ToString("N") + "@example.invalid";
        var now = DateTime.UtcNow;
        var first = new IdentityUser
        {
            Id = Guid.NewGuid(), NormalizedEmail = email,
            PasswordHash = "test-only-hash", CreatedAtUtc = now, IsActive = true,
            Roles = new List<IdentityUserRole>
            {
                new() { Role = IdentityRoleNames.Customer, AssignedAtUtc = now }
            }
        };
        db.IdentityUsers.Add(first);
        await db.SaveChangesAsync();

        db.IdentityUsers.Add(new IdentityUser
        {
            Id = Guid.NewGuid(), NormalizedEmail = email,
            PasswordHash = "test-only-hash", CreatedAtUtc = now, IsActive = true
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        // PostgreSQL aborts the transaction on uniqueness failure; disposing rolls it back.
    }
}
