using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase18: immutable per-operation authority environment. NULL is deliberate
/// for all historical Phase10 operations; never backfill or infer their origin.
/// Schema-only additive change; no authority IO or key changes.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261104230000_BindDispatchAuthorityEnvironment")]
public sealed class BindDispatchAuthorityEnvironment : Migration
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
    RAISE EXCEPTION 'Phase18 dispatch environment migration requires approved DB identity';
  END IF;
END $$;
ALTER TABLE invoice_dispatch_operations
  ADD COLUMN authority_environment integer NULL;
ALTER TABLE invoice_dispatch_operations
  ADD CONSTRAINT ck_p18_dispatch_authority_environment
    CHECK (authority_environment IS NULL OR authority_environment IN (1,2));
-- Historical operations stay explicitly unclassified; an environment is never
-- inferred from a JSON payload, a server setting, or a database hostname.
CREATE FUNCTION phase18_guard_dispatch_scope() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NEW.customer_user_id IS DISTINCT FROM OLD.customer_user_id
     OR NEW.taxpayer_id IS DISTINCT FROM OLD.taxpayer_id
     OR NEW.fiscal_memory_id IS DISTINCT FROM OLD.fiscal_memory_id
     OR NEW.invoice_id IS DISTINCT FROM OLD.invoice_id
     OR NEW.invoice_revision IS DISTINCT FROM OLD.invoice_revision
     OR NEW.authority_environment IS DISTINCT FROM OLD.authority_environment THEN
    RAISE EXCEPTION 'Phase18 dispatch authority scope is immutable';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_p18_dispatch_scope_immutable
  BEFORE UPDATE ON invoice_dispatch_operations
  FOR EACH ROW EXECUTE FUNCTION phase18_guard_dispatch_scope();
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Operation authority and evidence must not be destroyed by automatic rollback.");
}
