using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261013010000_AddTaxpayerProfiles")]
public sealed class AddTaxpayerProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "taxpayer_profiles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<int>(type: "integer", nullable: false),
                normalized_identifier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_taxpayer_profiles", x => x.id);
                table.CheckConstraint("ck_taxpayer_profiles_kind", "kind IN (1, 2)");
                table.CheckConstraint("ck_taxpayer_profiles_status", "status IN (1, 2, 3)");
                table.CheckConstraint("ck_taxpayer_profiles_identifier", "length(trim(normalized_identifier)) > 0");
                table.CheckConstraint("ck_taxpayer_profiles_display_name", "length(trim(display_name)) > 0");
                table.CheckConstraint("ck_taxpayer_profiles_revision", "revision > 0");
            });
        migrationBuilder.CreateIndex(
            name: "ux_taxpayer_profiles_kind_identifier",
            table: "taxpayer_profiles",
            columns: new[] { "kind", "normalized_identifier" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "taxpayer_profiles");
    }
}
