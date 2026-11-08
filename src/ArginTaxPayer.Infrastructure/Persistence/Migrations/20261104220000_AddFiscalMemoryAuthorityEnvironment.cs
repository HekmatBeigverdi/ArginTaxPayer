using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase18: immutable authority selection requires explicit review for each fiscal memory.
/// Legacy rows remain NULL (unclassified) and must not be routed automatically.
/// This migration changes schema only; no rows, keys, or dispatch operations are modified.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261104220000_AddFiscalMemoryAuthorityEnvironment")]
public sealed class AddFiscalMemoryAuthorityEnvironment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL lock_timeout = '5s';
            SET LOCAL statement_timeout = '60s';
            DO $$
            BEGIN
                IF NOT (
                    current_database() = 'argintaxpayer_test'
                    OR (current_database() = 'argin_phase18_acceptance'
                        AND current_user = 'phase18_acceptance'
                        AND EXISTS (SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
                    OR (current_database() = 'argintaxpayer_prod'
                        AND current_user = 'argin_prod_migrator'
                        AND EXISTS (SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
                ) THEN
                    RAISE EXCEPTION 'Phase18 fiscal environment migration requires approved database identity';
                END IF;
            END $$;
            """);
        migrationBuilder.AddColumn<int>(
            name: "authority_environment",
            table: "fiscal_memories",
            type: "integer",
            nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "ck_fiscal_memories_authority_environment",
            table: "fiscal_memories",
            sql: "authority_environment IS NULL OR authority_environment IN (1, 2)");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Authority environment classification must not be destroyed by automatic rollback.");
}
