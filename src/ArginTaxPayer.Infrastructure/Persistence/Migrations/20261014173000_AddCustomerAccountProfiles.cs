using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261014173000_AddCustomerAccountProfiles")]
public sealed class AddCustomerAccountProfiles : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("customer_account_profiles", t => new
        {
            customer_user_id = t.Column<Guid>(type: "uuid", nullable: false),
            display_name = t.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            status = t.Column<int>(type: "integer", nullable: false),
            preferred_party_id = t.Column<Guid>(type: "uuid", nullable: true),
            revision = t.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
            created_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_customer_account_profiles", x => x.customer_user_id);
            t.ForeignKey("FK_customer_account_profiles_identity", x => x.customer_user_id,
                "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_customer_account_profiles_party", x => x.preferred_party_id,
                "parties", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_customer_profile_status", "status IN (1, 2)");
            t.CheckConstraint("ck_customer_profile_revision", "revision > 0");
            t.CheckConstraint("ck_customer_profile_name", "length(trim(display_name)) > 0");
        });
        m.CreateIndex("ix_customer_account_profiles_party", "customer_account_profiles", "preferred_party_id");
    }

    protected override void Down(MigrationBuilder m) => m.DropTable("customer_account_profiles");
}
