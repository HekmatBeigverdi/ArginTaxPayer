using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261105020000_AddFiscalMemoryAuthorityEvents")]
public sealed class AddFiscalMemoryAuthorityEvents : Migration
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
          ) THEN RAISE EXCEPTION 'Fiscal memory authority events require approved DB identity'; END IF;
        END $$;
        CREATE TABLE fiscal_memory_authority_events (
          id uuid PRIMARY KEY,
          fiscal_memory_id uuid NOT NULL REFERENCES fiscal_memories(id) ON DELETE RESTRICT,
          taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
          actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          authority_environment integer NOT NULL CHECK(authority_environment IN (1,2)),
          action varchar(16) NOT NULL CHECK(action IN ('classify','activate')),
          from_status integer NOT NULL,
          to_status integer NOT NULL,
          memory_revision bigint NOT NULL CHECK(memory_revision > 1),
          recorded_at_utc timestamptz NOT NULL,
          UNIQUE(fiscal_memory_id,memory_revision)
        );
        CREATE FUNCTION guard_fiscal_memory_authority_events() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Fiscal memory authority evidence is append-only'; END $$;
        CREATE TRIGGER trg_fiscal_memory_authority_events_immutable
          BEFORE UPDATE OR DELETE ON fiscal_memory_authority_events
          FOR EACH ROW EXECUTE FUNCTION guard_fiscal_memory_authority_events();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Authority verification evidence cannot be removed by automatic rollback.");
}
