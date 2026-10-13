using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261013210000_AddFiscalMemories")]
public sealed class AddFiscalMemories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "fiscal_memories",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                taxpayer_id = table.Column<Guid>(type: "uuid", nullable: false),
                memory_identifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_fiscal_memories", x => x.id);
                table.ForeignKey("FK_fiscal_memories_taxpayer_profiles_taxpayer_id",
                    x => x.taxpayer_id, "taxpayer_profiles", "id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_fiscal_memories_status", "status IN (1, 2, 3, 4)");
                table.CheckConstraint("ck_fiscal_memories_identifier", "length(trim(memory_identifier)) > 0");
                table.CheckConstraint("ck_fiscal_memories_revision", "revision > 0");
            });
        migrationBuilder.CreateIndex(
            name: "ux_fiscal_memories_identifier", table: "fiscal_memories",
            column: "memory_identifier", unique: true);
        migrationBuilder.CreateIndex(
            name: "ux_fiscal_memories_active_taxpayer", table: "fiscal_memories",
            column: "taxpayer_id", unique: true, filter: "status = 2");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "fiscal_memories");
    }
}
