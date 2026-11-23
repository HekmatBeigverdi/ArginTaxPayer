using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261123140000_AddInvoiceDeletionMarkers")]
public sealed class AddInvoiceDeletionMarkers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "invoice_documents", "invoice_entry_workspaces" })
            migrationBuilder.AddColumn<DateTime>("deleted_at_utc", table,
                type: "timestamp with time zone", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("deleted_at_utc", "invoice_documents");
        migrationBuilder.DropColumn("deleted_at_utc", "invoice_entry_workspaces");
    }
}
