using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261015170000_AddPartyIdentityHistory")]
public sealed class AddPartyIdentityHistory : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("party_identity_history", t => new
        {
            id = t.Column<Guid>("uuid", nullable: false),
            party_id = t.Column<Guid>("uuid", nullable: false),
            actor_user_id = t.Column<Guid>("uuid", nullable: false),
            prior_revision = t.Column<long>("bigint", nullable: false),
            prior_kind = t.Column<int>("integer", nullable: false),
            prior_normalized_identifier = t.Column<string>("character varying(32)", maxLength: 32, nullable: false),
            prior_display_name = t.Column<string>("character varying(256)", maxLength: 256, nullable: false),
            prior_first_name = t.Column<string>("character varying(100)", maxLength: 100, nullable: true),
            prior_last_name = t.Column<string>("character varying(100)", maxLength: 100, nullable: true),
            prior_registration_number = t.Column<string>("character varying(32)", maxLength: 32, nullable: true),
            captured_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_party_identity_history", x => x.id);
            t.ForeignKey("FK_party_identity_history_parties_party_id", x => x.party_id,
                "parties", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_party_identity_history_identity_users_actor_user_id", x => x.actor_user_id,
                "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_party_identity_history_revision", "prior_revision > 0");
        });
        m.CreateIndex("ux_party_identity_history_revision", "party_identity_history",
            new[] { "party_id", "prior_revision" }, unique: true);
        m.CreateIndex("IX_party_identity_history_actor_user_id", "party_identity_history", "actor_user_id");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("party_identity_history");
}
