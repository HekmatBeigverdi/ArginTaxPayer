using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261111180000_AddFiscalMemoryConnectionObservations")]
public sealed class AddFiscalMemoryConnectionObservations : Migration
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
          ) THEN RAISE EXCEPTION 'Connection observations require approved DB identity'; END IF;
        END $$;
        CREATE TABLE fiscal_memory_connection_observations (
          attempt_id uuid PRIMARY KEY,
          customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
          memory_id uuid NOT NULL,
          source text NOT NULL CHECK(source IN ('RegisteredMemory','LegacySandbox')),
          binding jsonb NOT NULL,
          status text NOT NULL,
          started_at_utc timestamptz NOT NULL,
          tested_at_utc timestamptz NOT NULL
        );
        CREATE INDEX ix_memory_connection_observations_scope
          ON fiscal_memory_connection_observations(customer_user_id,taxpayer_id,memory_id,source,started_at_utc DESC,attempt_id DESC);
        CREATE FUNCTION protect_memory_connection_observations() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Connection observations are append-only'; END $$;
        CREATE TRIGGER protect_memory_connection_observations BEFORE UPDATE OR DELETE
          ON fiscal_memory_connection_observations FOR EACH ROW EXECUTE FUNCTION protect_memory_connection_observations();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Connection observations are retained as audit evidence.");
}
