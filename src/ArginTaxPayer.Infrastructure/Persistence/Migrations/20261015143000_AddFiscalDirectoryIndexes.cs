using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261015143000_AddFiscalDirectoryIndexes")]
public sealed class AddFiscalDirectoryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_parties_fiscal_directory",
            table: "parties",
            columns: new[] { "created_at_utc", "id" });
        migrationBuilder.CreateIndex(
            name: "ix_taxpayer_profiles_fiscal_directory",
            table: "taxpayer_profiles",
            columns: new[] { "created_at_utc", "id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("ix_parties_fiscal_directory", "parties");
        migrationBuilder.DropIndex("ix_taxpayer_profiles_fiscal_directory", "taxpayer_profiles");
    }
}
