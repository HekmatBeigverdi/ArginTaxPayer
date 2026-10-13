using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261012040000_AddIdentityMfa")]
public sealed class AddIdentityMfa : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "identity_mfa_states",
            columns: t => new {
                user_id = t.Column<Guid>(type: "uuid", nullable: false),
                encrypted_secret = t.Column<string>(type: "text", nullable: false),
                enabled_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_accepted_totp_step = t.Column<long>(type: "bigint", nullable: false, defaultValue: -1L),
                recovery_generation = t.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: t => {
                t.PrimaryKey("PK_identity_mfa_states", x => x.user_id);
                t.ForeignKey("FK_identity_mfa_states_identity_users_user_id", x => x.user_id,
                    "identity_users", "id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateTable(name: "identity_recovery_codes",
            columns: t => new {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                user_id = t.Column<Guid>(type: "uuid", nullable: false),
                code_hash = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                used_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: t => {
                t.PrimaryKey("PK_identity_recovery_codes", x => x.id);
                t.ForeignKey("FK_identity_recovery_codes_identity_users_user_id", x => x.user_id,
                    "identity_users", "id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateTable(name: "identity_mfa_challenges",
            columns: t => new {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                user_id = t.Column<Guid>(type: "uuid", nullable: false),
                challenge_hash = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                expires_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                consumed_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                attempt_count = t.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: t => {
                t.PrimaryKey("PK_identity_mfa_challenges", x => x.id);
                t.ForeignKey("FK_identity_mfa_challenges_identity_users_user_id", x => x.user_id,
                    "identity_users", "id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "ux_identity_recovery_user_hash",
            table: "identity_recovery_codes", columns: new[] { "user_id", "code_hash" }, unique: true);
        migrationBuilder.CreateIndex(name: "ux_identity_challenge_hash",
            table: "identity_mfa_challenges", column: "challenge_hash", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "identity_mfa_challenges");
        migrationBuilder.DropTable(name: "identity_recovery_codes");
        migrationBuilder.DropTable(name: "identity_mfa_states");
    }
}
