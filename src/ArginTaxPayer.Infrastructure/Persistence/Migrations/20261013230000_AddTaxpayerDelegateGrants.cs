using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261013230000_AddTaxpayerDelegateGrants")]
public sealed class AddTaxpayerDelegateGrants : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(
            name: "taxpayer_delegate_grants",
            columns: t => new
            {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                taxpayer_id = t.Column<Guid>(type: "uuid", nullable: false),
                delegate_user_id = t.Column<Guid>(type: "uuid", nullable: false),
                granted_by_level1_user_id = t.Column<Guid>(type: "uuid", nullable: false),
                granted_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                revoked_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                revoked_by_level1_user_id = t.Column<Guid>(type: "uuid", nullable: true),
                revocation_reason = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                revision = t.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_taxpayer_delegate_grants", x => x.id);
                t.ForeignKey("FK_delegate_taxpayer", x => x.taxpayer_id, "taxpayer_profiles", "id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_delegate_user", x => x.delegate_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_delegate_grantor", x => x.granted_by_level1_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_delegate_revoker", x => x.revoked_by_level1_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("ck_delegate_revision", "revision > 0");
                t.CheckConstraint("ck_delegate_revocation", "revoked_at_utc IS NULL OR (revoked_at_utc >= granted_at_utc AND revoked_by_level1_user_id IS NOT NULL AND length(trim(revocation_reason)) > 0)");
            });
        m.CreateIndex("ux_taxpayer_delegate_active", "taxpayer_delegate_grants",
            new[] { "taxpayer_id", "delegate_user_id" }, unique: true, filter: "revoked_at_utc IS NULL");
        m.CreateIndex("ix_delegate_current_scope", "taxpayer_delegate_grants",
            new[] { "delegate_user_id", "revoked_at_utc" });
        m.CreateIndex("IX_taxpayer_delegate_grants_granted_by_level1_user_id", "taxpayer_delegate_grants", "granted_by_level1_user_id");
        m.CreateIndex("IX_taxpayer_delegate_grants_revoked_by_level1_user_id", "taxpayer_delegate_grants", "revoked_by_level1_user_id");
    }

    protected override void Down(MigrationBuilder m) => m.DropTable("taxpayer_delegate_grants");
}
