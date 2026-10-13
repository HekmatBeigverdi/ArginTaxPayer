using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261012090000_AddMobileIdentityOtp")]
public sealed class AddMobileIdentityOtp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing email/password identities are not rewritten or deleted.
        migrationBuilder.AlterColumn<string>(name: "normalized_email", table: "identity_users",
            type: "character varying(320)", maxLength: 320, nullable: true,
            oldClrType: typeof(string), oldType: "character varying(320)", oldMaxLength: 320);
        migrationBuilder.AlterColumn<string>(name: "password_hash", table: "identity_users",
            type: "character varying(512)", maxLength: 512, nullable: true,
            oldClrType: typeof(string), oldType: "character varying(512)", oldMaxLength: 512);
        migrationBuilder.AddColumn<string>(name: "normalized_mobile", table: "identity_users",
            type: "character varying(13)", maxLength: 13, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "mobile_verified_at_utc", table: "identity_users",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex(name: "ux_identity_users_mobile", table: "identity_users",
            column: "normalized_mobile", unique: true);
        migrationBuilder.CreateTable(name: "identity_phone_challenges",
            columns: t => new {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                mobile = t.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                purpose = t.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                code_digest = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                user_id = t.Column<Guid>(type: "uuid", nullable: true),
                attempts = t.Column<int>(type: "integer", nullable: false),
                created_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                consumed_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                delivered = t.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: t => {
                t.PrimaryKey("PK_identity_phone_challenges", x => x.id);
                t.ForeignKey("FK_identity_phone_challenges_identity_users_user_id", x => x.user_id,
                    "identity_users", "id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "ix_phone_challenge_cooldown",
            table: "identity_phone_challenges", columns: new[] { "mobile", "purpose", "created_at_utc" });
        migrationBuilder.CreateIndex(name: "IX_identity_phone_challenges_user_id",
            table: "identity_phone_challenges", column: "user_id");
        migrationBuilder.Sql("""
            ALTER TABLE identity_users
              ADD CONSTRAINT ck_identity_contact_required
                CHECK (normalized_email IS NOT NULL OR normalized_mobile IS NOT NULL);
            ALTER TABLE identity_users
              ADD CONSTRAINT ck_identity_mobile_verified
                CHECK (normalized_mobile IS NULL OR mobile_verified_at_utc IS NOT NULL);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Downgrade requires every identity to have email/password and must be checked manually.
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM identity_users
                         WHERE normalized_email IS NULL OR password_hash IS NULL) THEN
                RAISE EXCEPTION 'Cannot downgrade mobile identities with passwordless accounts';
              END IF;
            END $$;
            ALTER TABLE identity_users DROP CONSTRAINT ck_identity_contact_required;
            ALTER TABLE identity_users DROP CONSTRAINT ck_identity_mobile_verified;
            """);
        migrationBuilder.DropTable(name: "identity_phone_challenges");
        migrationBuilder.DropIndex(name: "ux_identity_users_mobile", table: "identity_users");
        migrationBuilder.DropColumn(name: "normalized_mobile", table: "identity_users");
        migrationBuilder.DropColumn(name: "mobile_verified_at_utc", table: "identity_users");
        migrationBuilder.AlterColumn<string>(name: "normalized_email", table: "identity_users",
            type: "character varying(320)", maxLength: 320, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(320)", oldMaxLength: 320, oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "password_hash", table: "identity_users",
            type: "character varying(512)", maxLength: 512, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(512)", oldMaxLength: 512, oldNullable: true);
    }
}
