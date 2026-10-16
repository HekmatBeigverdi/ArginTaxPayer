using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialGoodsCodeFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_official_goods_code_format",
                table: "official_goods_service_entries",
                sql: "code ~ '^[0-9]{13}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_official_goods_code_format",
                table: "official_goods_service_entries");
        }
    }
}
