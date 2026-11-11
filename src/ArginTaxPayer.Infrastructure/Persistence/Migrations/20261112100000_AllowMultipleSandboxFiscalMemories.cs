using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261112100000_AllowMultipleSandboxFiscalMemories")]
public sealed class AllowMultipleSandboxFiscalMemories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
BEGIN
 IF current_database() <> 'argintaxpayer_test' AND NOT (
      current_database() = 'argin_phase18_acceptance' AND current_user = 'phase18_acceptance'
      AND EXISTS (SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl)
    ) AND NOT (
      current_database() = 'argintaxpayer_prod' AND current_user = 'argin_prod_migrator'
      AND EXISTS (SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl)
    ) THEN
   RAISE EXCEPTION 'Sandbox fiscal memory expansion restricted to approved databases';
 END IF;
 IF to_regclass('public.sandbox_fiscal_memories') IS NULL THEN
   RAISE EXCEPTION 'Sandbox metadata table missing';
 END IF;
 IF EXISTS (SELECT 1 FROM public.sandbox_fiscal_memories GROUP BY customer_user_id,taxpayer_id,memory_identifier HAVING count(*)>1) THEN
   RAISE EXCEPTION 'Duplicate Sandbox identifiers exist; reconcile before applying migration';
 END IF;
END $$;
ALTER TABLE public.sandbox_fiscal_memories
 DROP CONSTRAINT IF EXISTS uq_sandbox_owner_taxpayer;
CREATE UNIQUE INDEX IF NOT EXISTS ux_sandbox_owner_taxpayer_identifier
 ON public.sandbox_fiscal_memories(customer_user_id,taxpayer_id,memory_identifier);
CREATE INDEX IF NOT EXISTS ix_sandbox_memory_created
 ON public.sandbox_fiscal_memories(customer_user_id,taxpayer_id,created_at_utc,id);
""");

    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException(
        "Multiple Sandbox memories may already be referenced by invoices; use a reviewed forward migration.");
}
