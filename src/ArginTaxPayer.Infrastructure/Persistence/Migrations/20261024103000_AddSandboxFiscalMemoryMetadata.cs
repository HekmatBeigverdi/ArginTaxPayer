using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Original Sandbox metadata-only schema (already applied in operator test DB).\n/// Private key custody is introduced by the separate repair migration.\n/// No production fiscal_memories/signing keys touched.
/// Reviewed test database only; never reverse by data-destructive Down.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261024103000_AddSandboxFiscalMemoryMetadata")]
public sealed class AddSandboxFiscalMemoryMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
BEGIN
 IF current_database() <> 'argintaxpayer_test' THEN
   RAISE EXCEPTION 'Sandbox fiscal memory migration restricted to argintaxpayer_test';
 END IF;
END $$;
CREATE TABLE sandbox_fiscal_memories(
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 memory_identifier varchar(32) NOT NULL,
 public_spki_der bytea NOT NULL,
 public_fingerprint_sha256 varchar(64) NOT NULL,
 revision bigint NOT NULL DEFAULT 1,
 created_at_utc timestamptz NOT NULL,
 updated_at_utc timestamptz NOT NULL,
 CONSTRAINT ck_sandbox_memory_identifier CHECK(memory_identifier ~ '^[A-Z0-9]{6,32}$'),
 CONSTRAINT ck_sandbox_public_sha CHECK(public_fingerprint_sha256 ~ '^[0-9a-f]{64}$'),
 CONSTRAINT ck_sandbox_public_length CHECK(octet_length(public_spki_der) BETWEEN 250 AND 4096),
 CONSTRAINT ck_sandbox_revision CHECK(revision > 0),
 CONSTRAINT uq_sandbox_owner_taxpayer UNIQUE(customer_user_id,taxpayer_id)
);
CREATE INDEX ix_sandbox_memory_owner ON sandbox_fiscal_memories(customer_user_id,taxpayer_id);
""");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Sandbox credentials must not be destroyed by automatic rollback.");
}
