using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261013233000_AddCustomerCommercialOwnership")]
public sealed class AddCustomerCommercialOwnership : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<Guid>("customer_user_id", "taxpayer_profiles", type: "uuid", nullable: true);
        m.CreateIndex("ix_taxpayer_profiles_customer_user_id", "taxpayer_profiles", "customer_user_id");
        m.AddForeignKey("FK_taxpayer_profiles_customer_user_id", "taxpayer_profiles",
            "customer_user_id", "identity_users", "id", onDelete: ReferentialAction.Restrict);
        m.CreateTable("customer_commercial_owners", t => new
        {
            customer_user_id = t.Column<Guid>(type: "uuid", nullable: false),
            root_level1_user_id = t.Column<Guid>(type: "uuid", nullable: false),
            partner_level2_user_id = t.Column<Guid>(type: "uuid", nullable: true),
            acquisition_channel = t.Column<int>(type: "integer", nullable: false),
            revision = t.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
            created_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, t =>
        {
            t.PrimaryKey("PK_customer_commercial_owners", x => x.customer_user_id);
            t.ForeignKey("FK_customer_owner_customer", x => x.customer_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_customer_owner_root", x => x.root_level1_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_customer_owner_partner", x => x.partner_level2_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_customer_owner_revision", "revision > 0");
            t.CheckConstraint("ck_customer_owner_channel", "acquisition_channel IN (1, 2, 3, 4)");
            t.CheckConstraint("ck_customer_owner_not_root_partner", "partner_level2_user_id IS NULL OR partner_level2_user_id <> root_level1_user_id");
        });
        m.CreateIndex("IX_customer_commercial_owners_root_level1_user_id", "customer_commercial_owners", "root_level1_user_id");
        m.CreateIndex("IX_customer_commercial_owners_partner_level2_user_id", "customer_commercial_owners", "partner_level2_user_id");

        m.CreateTable("customer_commercial_owner_changes", t => new
        {
            id = t.Column<Guid>(type: "uuid", nullable: false),
            customer_user_id = t.Column<Guid>(type: "uuid", nullable: false),
            root_level1_user_id = t.Column<Guid>(type: "uuid", nullable: false),
            previous_partner_level2_user_id = t.Column<Guid>(type: "uuid", nullable: true),
            new_partner_level2_user_id = t.Column<Guid>(type: "uuid", nullable: true),
            approved_by_level1_user_id = t.Column<Guid>(type: "uuid", nullable: false),
            reason = t.Column<int>(type: "integer", nullable: false),
            explanation = t.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
            from_revision = t.Column<long>(type: "bigint", nullable: false),
            to_revision = t.Column<long>(type: "bigint", nullable: false),
            changed_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, t =>
        {
            t.PrimaryKey("PK_customer_commercial_owner_changes", x => x.id);
            t.ForeignKey("FK_customer_owner_change_owner", x => x.customer_user_id, "customer_commercial_owners", "customer_user_id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_customer_owner_change_actor", x => x.approved_by_level1_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_owner_changes_revision", "to_revision = from_revision + 1");
            t.CheckConstraint("ck_owner_changes_reason", "reason IN (1, 2, 3, 4)");
        });
        m.CreateIndex("IX_customer_commercial_owner_changes_approved_by_level1_user_id", "customer_commercial_owner_changes", "approved_by_level1_user_id");
        m.CreateIndex("IX_customer_commercial_owner_changes_customer_user_id_to_revision",
            "customer_commercial_owner_changes", new[] { "customer_user_id", "to_revision" }, unique: true);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropForeignKey("FK_taxpayer_profiles_customer_user_id", "taxpayer_profiles");
        m.DropIndex("ix_taxpayer_profiles_customer_user_id", "taxpayer_profiles");
        m.DropColumn("customer_user_id", "taxpayer_profiles");
        m.DropTable("customer_commercial_owner_changes");
        m.DropTable("customer_commercial_owners");
    }
}
