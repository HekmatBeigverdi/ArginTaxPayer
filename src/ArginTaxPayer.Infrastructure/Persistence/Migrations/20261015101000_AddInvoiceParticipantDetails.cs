using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261015101000_AddInvoiceParticipantDetails")]
public sealed class AddInvoiceParticipantDetails : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("invoice_participant_details", t => new
        {
            id = t.Column<Guid>("uuid", nullable: false),
            customer_user_id = t.Column<Guid>("uuid", nullable: false),
            party_id = t.Column<Guid?>("uuid", nullable: true),
            taxpayer_id = t.Column<Guid?>("uuid", nullable: true),
            economic_code = t.Column<string>("character varying(32)", maxLength: 32, nullable: true),
            branch_code = t.Column<string>("character varying(16)", maxLength: 16, nullable: true),
            postal_code = t.Column<string>("character varying(10)", maxLength: 10, nullable: true),
            province = t.Column<string>("character varying(120)", maxLength: 120, nullable: true),
            city = t.Column<string>("character varying(120)", maxLength: 120, nullable: true),
            address = t.Column<string>("character varying(500)", maxLength: 500, nullable: true),
            phone = t.Column<string>("character varying(20)", maxLength: 20, nullable: true),
            foreign_national_code = t.Column<string>("character varying(32)", maxLength: 32, nullable: true),
            passport_number = t.Column<string>("character varying(32)", maxLength: 32, nullable: true),
            contract_reference = t.Column<string>("character varying(64)", maxLength: 64, nullable: true),
            revision = t.Column<long>("bigint", nullable: false, defaultValue: 1L),
            created_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            updated_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false),
        }, constraints: t =>
        {
            t.PrimaryKey("PK_invoice_participant_details", x => x.id);
            t.ForeignKey("FK_invoice_participants_customer", x => x.customer_user_id,
                "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_invoice_participants_party", x => x.party_id,
                "parties", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_invoice_participants_taxpayer", x => x.taxpayer_id,
                "taxpayer_profiles", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_invoice_participant_owner",
                "(party_id IS NOT NULL AND taxpayer_id IS NULL) OR (party_id IS NULL AND taxpayer_id IS NOT NULL)");
            t.CheckConstraint("ck_invoice_participant_revision", "revision > 0");
        });
        m.CreateIndex("ix_invoice_participant_customer", "invoice_participant_details", "customer_user_id");
        m.CreateIndex("ux_invoice_participants_buyer", "invoice_participant_details",
            new[] { "customer_user_id", "party_id" }, unique: true, filter: "party_id IS NOT NULL");
        m.CreateIndex("ux_invoice_participants_seller", "invoice_participant_details",
            new[] { "customer_user_id", "taxpayer_id" }, unique: true, filter: "taxpayer_id IS NOT NULL");
        m.CreateIndex("IX_invoice_participant_details_party_id", "invoice_participant_details", "party_id");
        m.CreateIndex("IX_invoice_participant_details_taxpayer_id", "invoice_participant_details", "taxpayer_id");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("invoice_participant_details");
}
