using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ArginTaxPayer.Infrastructure.Persistence;

/// <summary>
/// Migration-only factory. Requires an explicit connection string and never
/// assumes Development/Production settings or hardcodes credentials.
/// </summary>
public sealed class ArginTaxPayerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ArginTaxPayerDbContext>
{
    public ArginTaxPayerDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException(
                "Set ConnectionStrings__ArginTaxPayerDb before running EF migration commands.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new ArginTaxPayerDbContext(options);
    }
}
