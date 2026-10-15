using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261014090000_AddPartiesAndCustomerPartyLinks")]
public sealed class AddPartiesAndCustomerPartyLinks : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // Additive only. No backfill, reassignment or modification of Phase04 relations.
        m.CreateTable(
            name: "parties",
            columns: t => new
            {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                kind = t.Column<int>(type: "integer", nullable: false),
                normalized_identifier = t.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                display_name = t.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                status = t.Column<int>(type: "integer", nullable: false),
                revision = t.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                created_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_parties", x => x.id);
                t.CheckConstraint("ck_parties_kind", "kind IN (1, 2)");
                t.CheckConstraint("ck_parties_status", "status IN (1, 2)");
                t.CheckConstraint("ck_parties_revision", "revision > 0");
                t.CheckConstraint("ck_parties_identifier", "length(trim(normalized_identifier)) > 0");
                t.CheckConstraint("ck_parties_name", "length(trim(display_name)) > 0");
            });
        m.CreateIndex("ux_parties_kind_identifier", "parties", new[] { "kind", "normalized_identifier" }, unique: true);

        m.CreateTable(
            name: "customer_party_links",
            columns: t => new
            {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                customer_user_id = t.Column<Guid>(type: "uuid", nullable: false),
                party_id = t.Column<Guid>(type: "uuid", nullable: false),
                linked_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                unlinked_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_customer_party_links", x => x.id);
                t.ForeignKey("FK_customer_party_links_customer", x => x.customer_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_customer_party_links_party", x => x.party_id, "parties", "id", onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("ck_customer_party_links_dates", "unlinked_at_utc IS NULL OR unlinked_at_utc >= linked_at_utc");
            });
        m.CreateIndex("ix_customer_party_links_customer", "customer_party_links", "customer_user_id");
        m.CreateIndex("ix_customer_party_links_party", "customer_party_links", "party_id");
        m.CreateIndex("ux_customer_party_links_active", "customer_party_links",
            new[] { "customer_user_id", "party_id" }, unique: true, filter: "unlinked_at_utc IS NULL");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("customer_party_links");
        m.DropTable("parties");
    }
}
