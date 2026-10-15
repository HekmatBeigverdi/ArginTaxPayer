using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261014110000_AddNaturalPersonNames")]
public sealed class AddNaturalPersonNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Additive only: no data rewrite or guessed splitting of historical display names.
        migrationBuilder.AddColumn<string>(
            name: "first_name", table: "parties", type: "character varying(100)",
            maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "last_name", table: "parties", type: "character varying(100)",
            maxLength: 100, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "first_name", table: "parties");
        migrationBuilder.DropColumn(name: "last_name", table: "parties");
    }
}
