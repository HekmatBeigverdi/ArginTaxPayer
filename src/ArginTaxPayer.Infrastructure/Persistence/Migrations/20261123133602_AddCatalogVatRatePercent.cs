using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogVatRatePercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate_percent",
                table: "customer_catalog_items",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_item_vat_rate",
                table: "customer_catalog_items",
                sql: "vat_rate_percent >= 0 AND vat_rate_percent <= 999.99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_catalog_item_vat_rate",
                table: "customer_catalog_items");

            migrationBuilder.DropColumn(
                name: "vat_rate_percent",
                table: "customer_catalog_items");
        }
    }
}
