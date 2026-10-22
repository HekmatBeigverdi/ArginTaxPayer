using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Explicit opt-in phase 09 test-only migration; never automatically applied.
/// Keeps AES-GCM encrypted envelopes only, not the KEK or plaintext PKCS8.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261022140000_AddEncryptedDatabaseSigningCustody")]
public sealed class AddEncryptedDatabaseSigningCustody : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
BEGIN
    IF current_database() <> 'argintaxpayer_test' THEN
        RAISE EXCEPTION 'Encrypted signing custody migration is restricted to argintaxpayer_test';
    END IF;
END $$;
CREATE TABLE signing_private_key_envelopes (
    key_version_id uuid PRIMARY KEY REFERENCES signing_key_versions(id) ON DELETE RESTRICT,
    customer_user_id uuid NOT NULL,
    taxpayer_id uuid NOT NULL,
    fiscal_memory_id uuid NOT NULL,
    origin_operation_record_id uuid NOT NULL,
    envelope_json text NOT NULL,
    version integer NOT NULL DEFAULT 1 CHECK (version >= 1),
    CONSTRAINT ck_signing_db_envelope_length CHECK (length(envelope_json) BETWEEN 200 AND 32768)
);
CREATE INDEX ix_signing_db_envelope_scope
ON signing_private_key_envelopes (customer_user_id, taxpayer_id, fiscal_memory_id);
REVOKE ALL ON TABLE signing_private_key_envelopes FROM PUBLIC;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentional safety: never silently destroy protected taxpayer private key custody.
        throw new NotSupportedException("Encrypted taxpayer signing envelopes require controlled export and verified migration before removal.");
    }
}
