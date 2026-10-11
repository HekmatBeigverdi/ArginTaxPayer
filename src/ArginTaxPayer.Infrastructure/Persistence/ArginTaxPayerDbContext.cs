using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence;

/// <summary>
/// Explicit migration-managed persistence context. No application code should
/// call EnsureCreated or apply migrations during API/Worker startup.
/// </summary>
public sealed class ArginTaxPayerDbContext(DbContextOptions<ArginTaxPayerDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // The baseline has no domain entities. Future schema mappings require review.
    }
}
