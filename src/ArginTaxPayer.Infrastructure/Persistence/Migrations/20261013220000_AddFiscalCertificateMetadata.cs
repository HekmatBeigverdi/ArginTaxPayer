using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261013220000_AddFiscalCertificateMetadata")]
public sealed class AddFiscalCertificateMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "fiscal_certificate_metadata",
            columns: t => new
            {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                fiscal_memory_id = t.Column<Guid>(type: "uuid", nullable: false),
                fingerprint_sha256 = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                issuer = t.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                not_before_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                not_after_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                status = t.Column<int>(type: "integer", nullable: false),
                origin = t.Column<int>(type: "integer", nullable: false),
                custody_mode = t.Column<int>(type: "integer", nullable: false),
                custody_reference = t.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                created_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                superseded_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_fiscal_certificate_metadata", x => x.id);
                t.ForeignKey("FK_fiscal_certificate_metadata_fiscal_memories_fiscal_memory_id",
                    x => x.fiscal_memory_id, "fiscal_memories", "id",
                    onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("ck_fiscal_certificate_fingerprint", "fingerprint_sha256 ~ '^[0-9a-f]{64}$'");
                t.CheckConstraint("ck_fiscal_certificate_status", "status IN (1, 2, 3, 4)");
                t.CheckConstraint("ck_fiscal_certificate_origin", "origin IN (1, 2)");
                t.CheckConstraint("ck_fiscal_certificate_custody", "custody_mode = 1");
                t.CheckConstraint("ck_fiscal_certificate_validity", "not_before_utc IS NULL OR not_after_utc IS NULL OR not_before_utc < not_after_utc");
            });
        migrationBuilder.CreateIndex("ux_fiscal_certificate_current_memory", "fiscal_certificate_metadata",
            "fiscal_memory_id", unique: true, filter: "superseded_at_utc IS NULL");
        migrationBuilder.CreateIndex("ix_fiscal_certificate_history", "fiscal_certificate_metadata",
            new[] { "fiscal_memory_id", "created_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("fiscal_certificate_metadata");
}
