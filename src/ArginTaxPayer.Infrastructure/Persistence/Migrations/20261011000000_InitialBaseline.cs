using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Intentionally empty first migration. The only initial database object
/// provisioned by EF migration execution is __EFMigrationsHistory.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261011000000_InitialBaseline")]
public sealed class InitialBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Phase 02 has not yet approved any physical domain tables.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No domain schema to roll back.
    }

    protected override void BuildTargetModel(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "9.0.4");
    }
}
