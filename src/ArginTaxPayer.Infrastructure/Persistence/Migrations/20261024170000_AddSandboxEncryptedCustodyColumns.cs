using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Forward-only repair for the Sandbox metadata migration already applied
/// before encrypted key custody columns were introduced. No existing columns
/// or rows are deleted, and no production signing/fiscal tables are modified.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261024170000_AddSandboxEncryptedCustodyColumns")]
public sealed class AddSandboxEncryptedCustodyColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
BEGIN
 IF current_database() <> 'argintaxpayer_test' THEN
   RAISE EXCEPTION 'Sandbox encrypted custody repair restricted to argintaxpayer_test';
 END IF;
 IF to_regclass('public.sandbox_fiscal_memories') IS NULL THEN
   RAISE EXCEPTION 'Sandbox metadata table absent; do not repair missing history';
 END IF;
 IF EXISTS (
  SELECT 1 FROM information_schema.columns
  WHERE table_schema='public' AND table_name='sandbox_fiscal_memories'
    AND column_name IN ('private_envelope_json','signing_key_version_id','origin_operation_id')
 ) THEN
   RAISE EXCEPTION 'Sandbox custody columns partially exist: stop and reconcile manually';
 END IF;
END $$;

ALTER TABLE public.sandbox_fiscal_memories
 ADD COLUMN private_envelope_json text NULL,
 ADD COLUMN signing_key_version_id uuid NULL,
 ADD COLUMN origin_operation_id uuid NULL;

-- Existing Sandbox metadata rows have never had server-side private custody.
-- Generate distinct identities for those existing rows without overwriting
-- identifier/public key/fingerprint/revision or any preexisting secret.
UPDATE public.sandbox_fiscal_memories
SET signing_key_version_id=gen_random_uuid(),
    origin_operation_id=gen_random_uuid()
WHERE signing_key_version_id IS NULL AND origin_operation_id IS NULL;

ALTER TABLE public.sandbox_fiscal_memories
 ALTER COLUMN signing_key_version_id SET NOT NULL,
 ALTER COLUMN origin_operation_id SET NOT NULL;

ALTER TABLE public.sandbox_fiscal_memories
 ADD CONSTRAINT ck_sandbox_envelope
 CHECK (private_envelope_json IS NULL OR
        length(private_envelope_json) BETWEEN 1 AND 30000);
""");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Encrypted Sandbox custody requires separate retention-preserving rollback approval.");
}
