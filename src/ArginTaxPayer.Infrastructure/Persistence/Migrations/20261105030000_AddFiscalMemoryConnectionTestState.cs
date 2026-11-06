using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261105030000_AddFiscalMemoryConnectionTestState")]
public sealed class AddFiscalMemoryConnectionTestState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        SET LOCAL lock_timeout = '5s';
        SET LOCAL statement_timeout = '60s';
        DO $$ BEGIN
          IF NOT (
            current_database()='argintaxpayer_test'
            OR (current_database()='argin_phase18_acceptance' AND current_user='phase18_acceptance'
                AND EXISTS(SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
            OR (current_database()='argintaxpayer_prod' AND current_user='argin_prod_migrator'
                AND EXISTS(SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
          ) THEN RAISE EXCEPTION 'Fiscal memory connection state requires approved DB identity'; END IF;
        END $$;
        ALTER TABLE fiscal_memories
          ADD COLUMN last_connection_test_succeeded boolean NULL,
          ADD COLUMN last_connection_test_at_utc timestamptz NULL,
          ADD CONSTRAINT ck_fiscal_memories_connection_test CHECK (
            (last_connection_test_succeeded IS NULL AND last_connection_test_at_utc IS NULL)
            OR (last_connection_test_succeeded IS NOT NULL AND last_connection_test_at_utc IS NOT NULL));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Connection test history is retained to preserve audit context.");
}
