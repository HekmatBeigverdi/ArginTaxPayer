using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

// Retained as a no-op because this migration was briefly applied in local
// development. Rejected resubmissions are not a legal-chain relation.
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261107110000_AddRejectedInvoiceResubmissionLinks")]
public sealed class AddRejectedInvoiceResubmissionLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
