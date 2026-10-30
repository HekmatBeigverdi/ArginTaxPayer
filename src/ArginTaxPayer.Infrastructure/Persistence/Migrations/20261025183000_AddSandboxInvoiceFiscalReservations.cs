using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Additive sandbox-only durable fiscal serial reservations. No provider
/// adapter uses this table until the independently approved send gate ships.
/// Never update production databases via this provisional migration.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261025183000_AddSandboxInvoiceFiscalReservations")]
public sealed class AddSandboxInvoiceFiscalReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$
BEGIN
 IF current_database()<>'argintaxpayer_test' THEN
   RAISE EXCEPTION 'Sandbox fiscal reservation migration requires argintaxpayer_test';
 END IF;
END $$;
CREATE TABLE sandbox_invoice_fiscal_reservations (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 sandbox_memory_id uuid NOT NULL REFERENCES sandbox_fiscal_memories(id) ON DELETE RESTRICT,
 invoice_id uuid NOT NULL,
 invoice_revision bigint NOT NULL CHECK(invoice_revision>0),
 approved_snapshot_sha256 varchar(64) NOT NULL CHECK(approved_snapshot_sha256~'^[0-9a-f]{64}$'),
 serial bigint NOT NULL CHECK(serial BETWEEN 1 AND 1099511627775),
 fiscal_tax_id varchar(22) NOT NULL CHECK(fiscal_tax_id~'^[A-Z0-9]{22}$'),
 issue_date_utc timestamptz NOT NULL,
 created_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_sandbox_invoice_scope FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
 REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ux_sandbox_invoice_once UNIQUE(customer_user_id,taxpayer_id,invoice_id),
 CONSTRAINT ux_sandbox_memory_serial UNIQUE(sandbox_memory_id,serial),
 CONSTRAINT ux_sandbox_memory_taxid UNIQUE(sandbox_memory_id,fiscal_tax_id)
);
CREATE FUNCTION sandbox_fiscal_reservation_no_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Sandbox invoice fiscal reservation is immutable';
END $$;
CREATE TRIGGER trg_sandbox_invoice_fiscal_immutable BEFORE UPDATE OR DELETE
 ON sandbox_invoice_fiscal_reservations
 FOR EACH ROW EXECUTE FUNCTION sandbox_fiscal_reservation_no_mutation();
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Sandbox fiscal ID reservations are immutable: destructive rollback requires separate review.");
    }
}
