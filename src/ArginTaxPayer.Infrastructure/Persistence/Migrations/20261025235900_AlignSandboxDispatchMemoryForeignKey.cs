using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Legacy Sandbox-only dispatch FK alignment. Stops if existing operations
/// require a separate reconciliation; never deletes or remaps evidence.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261025235900_AlignSandboxDispatchMemoryForeignKey")]
public sealed class AlignSandboxDispatchMemoryForeignKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$
DECLARE old_constraint text;
BEGIN
 IF current_database() <> 'argintaxpayer_test' THEN
   RAISE EXCEPTION 'Sandbox dispatch FK update only permits argintaxpayer_test';
 END IF;
 LOCK TABLE invoice_dispatch_operations IN ACCESS EXCLUSIVE MODE;
 IF EXISTS(SELECT 1 FROM invoice_dispatch_operations) THEN
   RAISE EXCEPTION 'Existing dispatch evidence detected; manual retention-safe reconciliation required';
 END IF;
 SELECT c.conname INTO old_constraint
 FROM pg_constraint c
 JOIN pg_class t ON t.oid=c.conrelid
 JOIN pg_class target ON target.oid=c.confrelid
 WHERE t.relname='invoice_dispatch_operations' AND c.contype='f'
   AND target.relname='fiscal_memories'
   AND c.conkey=ARRAY[
      (SELECT attnum FROM pg_attribute
       WHERE attrelid=t.oid AND attname='fiscal_memory_id')
   ]::smallint[];
 IF old_constraint IS NULL THEN
   RAISE EXCEPTION 'Expected production-memory FK not found; schema drift detected';
 END IF;
 EXECUTE format('ALTER TABLE invoice_dispatch_operations DROP CONSTRAINT %I', old_constraint);
 ALTER TABLE invoice_dispatch_operations
   ADD CONSTRAINT fk_phase10_dispatch_sandbox_memory
   FOREIGN KEY(fiscal_memory_id) REFERENCES sandbox_fiscal_memories(id)
   ON DELETE RESTRICT;
END $$;
""");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Dispatch-memory evidence needs a separately approved retention-safe rollback.");
    }
}
