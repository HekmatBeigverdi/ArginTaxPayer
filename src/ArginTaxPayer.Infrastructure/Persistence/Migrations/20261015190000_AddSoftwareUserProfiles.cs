using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261015190000_AddSoftwareUserProfiles")]
public sealed class AddSoftwareUserProfiles : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("software_user_profiles", t => new
        {
            user_id = t.Column<Guid>("uuid", nullable: false),
            first_name = t.Column<string>("character varying(100)", maxLength: 100, nullable: false),
            last_name = t.Column<string>("character varying(100)", maxLength: 100, nullable: false),
            company_name = t.Column<string>("character varying(256)", maxLength: 256, nullable: true),
            address = t.Column<string>("character varying(500)", maxLength: 500, nullable: false),
            invoice_logo_png = t.Column<byte[]>("bytea", nullable: true),
            revision = t.Column<long>("bigint", nullable: false, defaultValue: 1L),
            completed_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            updated_at_utc = t.Column<DateTime>("timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_software_user_profiles", x => x.user_id);
            t.ForeignKey("FK_software_user_profiles_identity_users_user_id", x => x.user_id,
                "identity_users", "id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("ck_software_user_profiles_revision", "revision > 0");
            t.CheckConstraint("ck_software_user_profiles_name",
                "length(trim(first_name)) >= 2 AND length(trim(last_name)) >= 2");
            t.CheckConstraint("ck_software_user_profiles_address", "length(trim(address)) >= 2");
        });
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("software_user_profiles");
}
