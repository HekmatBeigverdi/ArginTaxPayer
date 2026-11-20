using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCatalogOfficialMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_official_mappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_official_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    official_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_official_mappings", x => x.id);
                    table.CheckConstraint("ck_catalog_mapping_interval", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
                    table.CheckConstraint("ck_catalog_mapping_revision", "revision > 0");
                    table.ForeignKey(
                        name: "FK_catalog_official_mappings_customer_catalog_items_item_id",
                        column: x => x.item_id,
                        principalTable: "customer_catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_official_mappings_official_goods_service_entries_of~",
                        column: x => x.official_entry_id,
                        principalTable: "official_goods_service_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_official_mappings_official_entry_id",
                table: "catalog_official_mappings",
                column: "official_entry_id");

            migrationBuilder.CreateIndex(
                name: "ux_catalog_mapping_item_from",
                table: "catalog_official_mappings",
                columns: new[] { "item_id", "effective_from_utc" },
                unique: true);
        }
    }
}
