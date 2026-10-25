using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>Additive test-db-only immutable audit for internal invoice lifecycle changes.</summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261025230000_AddSandboxInvoiceLifecycleEvents")]
public sealed class AddSandboxInvoiceLifecycleEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$
BEGIN
 IF current_database()<>'argintaxpayer_test' THEN
  RAISE EXCEPTION 'Invoice lifecycle audit migration only permits argintaxpayer_test';
 END IF;
END $$;
CREATE TABLE sandbox_invoice_lifecycle_events (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 invoice_id uuid NOT NULL,
 invoice_revision bigint NOT NULL CHECK(invoice_revision>0),
 from_state integer NOT NULL CHECK(from_state BETWEEN 1 AND 3),
 to_state integer NOT NULL CHECK(to_state BETWEEN 1 AND 3),
 snapshot_sha256 varchar(64) NOT NULL CHECK(snapshot_sha256~'^[0-9a-f]{64}$'),
 actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 created_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_sandbox_lifecycle_invoice FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
  REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ck_sandbox_lifecycle_transition CHECK(from_state<>to_state),
 CONSTRAINT ux_sandbox_lifecycle_transition UNIQUE(customer_user_id,taxpayer_id,invoice_id,invoice_revision,to_state)
);
CREATE INDEX ix_sandbox_lifecycle_invoice ON sandbox_invoice_lifecycle_events(invoice_id,created_at_utc);
CREATE FUNCTION sandbox_invoice_lifecycle_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Sandbox lifecycle history is append-only';
END $$;
CREATE TRIGGER trg_sandbox_invoice_lifecycle_immutable BEFORE UPDATE OR DELETE
 ON sandbox_invoice_lifecycle_events FOR EACH ROW EXECUTE FUNCTION sandbox_invoice_lifecycle_immutable();
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Sandbox invoice lifecycle audit is append-only; destructive rollback is prohibited.");
    }
}
