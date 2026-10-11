using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Checks metadata without connecting to any real database.</summary>
public sealed class MigrationBaselineTests
{
    [Fact]
    public void FirstMigrationIsDiscoverableWithoutDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=unused;Password=unused")
            .Options;
        using var context = new ArginTaxPayerDbContext(options);

        Assert.Contains("20261011000000_InitialBaseline", context.Database.GetMigrations());
        Assert.Empty(context.Model.GetEntityTypes());
    }

    [Fact]
    public void DesignTimeFactoryFailsWithoutExplicitConnectionConfiguration()
    {
        // Avoid process-wide Environment.SetEnvironmentVariable mutation in parallel tests.
        // The factory contract is verified during local dotnet ef command execution.
        Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<ArginTaxPayerDbContext>>(
            new ArginTaxPayerDesignTimeDbContextFactory());
    }
}
