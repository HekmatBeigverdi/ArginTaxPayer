using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261013200000_AddTaxpayerProfileHistory")]
public sealed class AddTaxpayerProfileHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "taxpayer_profile_changes",
            columns: t => new
            {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                taxpayer_id = t.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = t.Column<Guid>(type: "uuid", nullable: false),
                from_revision = t.Column<long>(type: "bigint", nullable: false),
                to_revision = t.Column<long>(type: "bigint", nullable: false),
                prior_display_name = t.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                new_display_name = t.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                prior_status = t.Column<int>(type: "integer", nullable: false),
                new_status = t.Column<int>(type: "integer", nullable: false),
                changed_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_taxpayer_profile_changes", x => x.id);
                t.ForeignKey("FK_taxpayer_profile_changes_taxpayer_profiles_taxpayer_id", x => x.taxpayer_id,
                    "taxpayer_profiles", "id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_taxpayer_profile_changes_identity_users_actor_user_id", x => x.actor_user_id,
                    "identity_users", "id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("ux_taxpayer_profile_changes_revision", "taxpayer_profile_changes",
            new[] { "taxpayer_id", "to_revision" }, unique: true);
        migrationBuilder.CreateIndex("IX_taxpayer_profile_changes_actor_user_id",
            "taxpayer_profile_changes", "actor_user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "taxpayer_profile_changes");
    }
}
