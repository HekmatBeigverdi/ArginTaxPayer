using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261014160000_AddPartyContactsAndAddresses")]
public sealed class AddPartyContactsAndAddresses : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("party_contacts", t => new
        {
            id = t.Column<Guid>("uuid", nullable: false),
            party_id = t.Column<Guid>("uuid", nullable: false),
            kind = t.Column<int>("integer", nullable: false),
            value = t.Column<string>("character varying(320)", maxLength: 320, nullable: false),
            contact_person = t.Column<string>("character varying(120)", maxLength: 120, nullable: true),
            is_primary = t.Column<bool>("boolean", nullable: false),
            is_active = t.Column<bool>("boolean", nullable: false),
            revision = t.Column<long>("bigint", nullable: false, defaultValue: 1L),
            created_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            updated_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            deactivated_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: true)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_party_contacts", x => x.id);
            t.ForeignKey("FK_party_contacts_party", x => x.party_id, "parties", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_party_contact_kind", "kind IN (1,2,3)");
            t.CheckConstraint("ck_party_contact_revision", "revision > 0");
            t.CheckConstraint("ck_party_contact_value", "length(trim(value)) > 0");
            t.CheckConstraint("ck_party_contact_deactivation", "(is_active AND deactivated_at_utc IS NULL) OR (NOT is_active AND deactivated_at_utc IS NOT NULL)");
        });
        m.CreateIndex("IX_party_contacts_party_id", "party_contacts", "party_id");
        m.CreateIndex("ux_party_contacts_active_value", "party_contacts",
            new[] { "party_id", "kind", "value" }, unique: true, filter: "is_active");
        m.CreateIndex("ux_party_contacts_primary", "party_contacts",
            new[] { "party_id", "kind" }, unique: true, filter: "is_active AND is_primary");

        m.CreateTable("party_addresses", t => new
        {
            id = t.Column<Guid>("uuid", nullable: false),
            party_id = t.Column<Guid>("uuid", nullable: false),
            label = t.Column<string>("character varying(80)", maxLength: 80, nullable: false),
            country_code = t.Column<string>("character varying(2)", maxLength: 2, nullable: false),
            province = t.Column<string>("character varying(120)", maxLength: 120, nullable: false),
            city = t.Column<string>("character varying(120)", maxLength: 120, nullable: false),
            street_address = t.Column<string>("character varying(500)", maxLength: 500, nullable: false),
            postal_code = t.Column<string>("character varying(20)", maxLength: 20, nullable: true),
            is_primary = t.Column<bool>("boolean", nullable: false),
            is_active = t.Column<bool>("boolean", nullable: false),
            revision = t.Column<long>("bigint", nullable: false, defaultValue: 1L),
            created_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            updated_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            deactivated_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: true)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_party_addresses", x => x.id);
            t.ForeignKey("FK_party_addresses_party", x => x.party_id, "parties", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_party_address_revision", "revision > 0");
            t.CheckConstraint("ck_party_address_deactivation", "(is_active AND deactivated_at_utc IS NULL) OR (NOT is_active AND deactivated_at_utc IS NOT NULL)");
        });
        m.CreateIndex("IX_party_addresses_party_id", "party_addresses", "party_id");
        m.CreateIndex("ux_party_addresses_primary", "party_addresses", "party_id",
            unique: true, filter: "is_active AND is_primary");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("party_contacts");
        m.DropTable("party_addresses");
    }
}
