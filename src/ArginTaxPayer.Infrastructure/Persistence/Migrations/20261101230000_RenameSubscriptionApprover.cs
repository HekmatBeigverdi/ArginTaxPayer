using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101230000_RenameSubscriptionApprover")]
public sealed class RenameSubscriptionApprover : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial_order_activations
          RENAME COLUMN activated_by_level1_user_id TO approved_by_user_id;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial_order_activations
          RENAME COLUMN approved_by_user_id TO activated_by_level1_user_id;
        """);
}
