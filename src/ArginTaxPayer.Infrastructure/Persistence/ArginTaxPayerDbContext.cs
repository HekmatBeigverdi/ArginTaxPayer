using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence;

/// <summary>
/// Persistence foundation only. Domain entities and schema mappings are introduced in later steps.
/// Never enable implicit database creation or migrations from a web request.
/// </summary>
public sealed class ArginTaxPayerDbContext(DbContextOptions<ArginTaxPayerDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Bounded-context-specific entity configurations and migrations follow in Step 07+.
    }
}
