using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261014190000_AddCustomerTaxpayerMemberships")]
public sealed class AddCustomerTaxpayerMemberships : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("customer_taxpayer_memberships", t => new
        {
            id = t.Column<Guid>("uuid", nullable: false),
            customer_user_id = t.Column<Guid>("uuid", nullable: false),
            taxpayer_id = t.Column<Guid>("uuid", nullable: false),
            linked_by_user_id = t.Column<Guid>("uuid", nullable: false),
            linked_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            unlinked_by_user_id = t.Column<Guid>("uuid", nullable: true),
            unlinked_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: true),
            revision = t.Column<long>("bigint", nullable: false, defaultValue: 1L)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_customer_taxpayer_memberships", x => x.id);
            t.ForeignKey("FK_customer_taxpayer_memberships_customer", x => x.customer_user_id,
                "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_customer_taxpayer_memberships_taxpayer", x => x.taxpayer_id,
                "taxpayer_profiles", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_customer_taxpayer_memberships_linked_by", x => x.linked_by_user_id,
                "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_customer_taxpayer_memberships_unlinked_by", x => x.unlinked_by_user_id,
                "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_customer_taxpayer_memberships_revision", "revision > 0");
            t.CheckConstraint("ck_customer_taxpayer_memberships_unlink",
                "(unlinked_at_utc IS NULL AND unlinked_by_user_id IS NULL) OR (unlinked_at_utc IS NOT NULL AND unlinked_by_user_id IS NOT NULL AND unlinked_at_utc >= linked_at_utc)");
        });
        m.CreateIndex("ux_customer_taxpayer_memberships_active", "customer_taxpayer_memberships",
            new[] { "customer_user_id", "taxpayer_id" }, unique: true, filter: "unlinked_at_utc IS NULL");
        m.CreateIndex("ix_customer_taxpayer_memberships_history", "customer_taxpayer_memberships",
            new[] { "customer_user_id", "linked_at_utc" });
        m.CreateIndex("IX_customer_taxpayer_memberships_taxpayer_id", "customer_taxpayer_memberships", "taxpayer_id");
        m.CreateIndex("IX_customer_taxpayer_memberships_linked_by_user_id", "customer_taxpayer_memberships", "linked_by_user_id");
        m.CreateIndex("IX_customer_taxpayer_memberships_unlinked_by_user_id", "customer_taxpayer_memberships", "unlinked_by_user_id");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("customer_taxpayer_memberships");
}
