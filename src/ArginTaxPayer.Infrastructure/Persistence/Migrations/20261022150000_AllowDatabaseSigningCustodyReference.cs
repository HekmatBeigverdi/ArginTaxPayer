using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>Allows the journal to persist database-custody locators alongside vault locators.</summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261022150000_AllowDatabaseSigningCustodyReference")]
public sealed class AllowDatabaseSigningCustodyReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
BEGIN
    IF current_database() <> 'argintaxpayer_test' THEN
        RAISE EXCEPTION 'Database custody constraint migration is restricted to argintaxpayer_test';
    END IF;
END $$;
ALTER TABLE signing_key_versions
    DROP CONSTRAINT IF EXISTS ck_p09_key_5;
ALTER TABLE signing_key_versions
    ADD CONSTRAINT ck_p09_key_5
    CHECK (custody_reference ~ '^(vault|db)://[A-Za-z0-9/_-]{1,248}$');
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A downgrade would reject already-created db:// custody references and is therefore unsafe.
        throw new NotSupportedException("Database signing custody references require controlled migration before downgrade.");
    }
}
