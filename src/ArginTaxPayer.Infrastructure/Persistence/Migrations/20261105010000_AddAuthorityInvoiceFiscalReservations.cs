using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261105010000_AddAuthorityInvoiceFiscalReservations")]
public sealed class AddAuthorityInvoiceFiscalReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$ BEGIN
 IF NOT (
    current_database() = 'argintaxpayer_test'
    OR (current_database() = 'argin_phase18_acceptance'
        AND current_user = 'phase18_acceptance'
        AND EXISTS (SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
    OR (current_database() = 'argintaxpayer_prod'
        AND current_user = 'argin_prod_migrator'
        AND EXISTS (SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
 ) THEN
   RAISE EXCEPTION 'Authority fiscal reservations require approved database identity';
 END IF;
END $$;
CREATE TABLE authority_invoice_fiscal_reservations (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 fiscal_memory_id uuid NOT NULL REFERENCES fiscal_memories(id) ON DELETE RESTRICT,
 invoice_id uuid NOT NULL,
 invoice_revision bigint NOT NULL CHECK(invoice_revision>0),
 approved_snapshot_sha256 varchar(64) NOT NULL CHECK(approved_snapshot_sha256~'^[0-9a-f]{64}$'),
 serial bigint NOT NULL CHECK(serial BETWEEN 1 AND 1099511627775),
 fiscal_tax_id varchar(22) NOT NULL CHECK(fiscal_tax_id~'^[A-Z0-9]{22}$'),
 issue_date_utc timestamptz NOT NULL,
 created_at_utc timestamptz NOT NULL,
 authority_environment integer NOT NULL CHECK(authority_environment IN (1,2)),
 CONSTRAINT fk_production_invoice_scope FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
   REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ux_production_invoice_once UNIQUE(customer_user_id,taxpayer_id,invoice_id),
 CONSTRAINT ux_production_memory_serial UNIQUE(fiscal_memory_id,serial),
 CONSTRAINT ux_production_memory_taxid UNIQUE(fiscal_memory_id,fiscal_tax_id)
);
CREATE FUNCTION authority_fiscal_reservation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP <> 'INSERT' THEN
   RAISE EXCEPTION 'Production fiscal reservation is immutable';
 END IF;
 IF NOT EXISTS (SELECT 1 FROM fiscal_memories m JOIN taxpayer_profiles t ON t.id=m.taxpayer_id
   JOIN invoice_documents i ON i.id=NEW.invoice_id AND i.taxpayer_id=t.id
     AND i.customer_user_id=t.customer_user_id
   JOIN invoice_document_revisions r ON r.invoice_id=i.id AND r.revision=i.revision
     AND r.customer_user_id=i.customer_user_id AND r.taxpayer_id=i.taxpayer_id
   WHERE m.id=NEW.fiscal_memory_id AND m.authority_environment=NEW.authority_environment AND m.status=2
     AND t.id=NEW.taxpayer_id AND t.customer_user_id=NEW.customer_user_id
     AND i.state=3 AND i.subject=1 AND i.revision=NEW.invoice_revision
     AND i.snapshot_sha256=NEW.approved_snapshot_sha256
     AND r.snapshot_sha256=i.snapshot_sha256 AND r.snapshot_json=i.snapshot_json
     AND i.snapshot_json::jsonb->>'environment'=CASE NEW.authority_environment WHEN 1 THEN 'Production' ELSE 'Sandbox' END
     AND i.snapshot_json::jsonb->>'fiscalMemoryId'=m.id::text
     AND left(NEW.fiscal_tax_id,6)=m.memory_identifier
     AND EXISTS(SELECT 1 FROM customer_taxpayer_memberships c
       WHERE c.customer_user_id=NEW.customer_user_id AND c.taxpayer_id=NEW.taxpayer_id
         AND c.unlinked_at_utc IS NULL)
     AND EXISTS(SELECT 1 FROM invoice_reviews v
       WHERE v.invoice_id=i.id AND v.customer_user_id=i.customer_user_id
         AND v.taxpayer_id=i.taxpayer_id AND v.state=4)
 ) THEN RAISE EXCEPTION 'Production fiscal reservation scope is not verified'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER trg_authority_fiscal_reservation_guard BEFORE INSERT OR UPDATE OR DELETE
 ON authority_invoice_fiscal_reservations
 FOR EACH ROW EXECUTE FUNCTION authority_fiscal_reservation_guard();

-- A real FK bridge preserves the legacy Sandbox FK while admitting classified
-- fiscal_memories. Existing evidence retains its original legacy memory link.
CREATE TABLE invoice_dispatch_memory_scopes (
 id uuid PRIMARY KEY,
 legacy_sandbox_memory_id uuid NULL REFERENCES sandbox_fiscal_memories(id) ON DELETE RESTRICT,
 classified_fiscal_memory_id uuid NULL REFERENCES fiscal_memories(id) ON DELETE RESTRICT,
 CONSTRAINT ck_dispatch_memory_source CHECK (
   num_nonnulls(legacy_sandbox_memory_id,classified_fiscal_memory_id)=1
   AND id=COALESCE(legacy_sandbox_memory_id,classified_fiscal_memory_id))
);
INSERT INTO invoice_dispatch_memory_scopes(id,legacy_sandbox_memory_id)
 SELECT DISTINCT fiscal_memory_id,fiscal_memory_id FROM invoice_dispatch_operations;
ALTER TABLE invoice_dispatch_operations DROP CONSTRAINT fk_phase10_dispatch_sandbox_memory;
ALTER TABLE invoice_dispatch_operations ADD CONSTRAINT fk_dispatch_memory_scope
 FOREIGN KEY(fiscal_memory_id) REFERENCES invoice_dispatch_memory_scopes(id) ON DELETE RESTRICT;
CREATE FUNCTION authority_dispatch_memory_scope_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_TABLE_NAME='invoice_dispatch_memory_scopes' THEN
   RAISE EXCEPTION 'Dispatch memory source is immutable';
 END IF;
 IF NEW.authority_environment IS NULL THEN
   IF NOT EXISTS(SELECT 1 FROM sandbox_fiscal_memories m WHERE m.id=NEW.fiscal_memory_id
     AND m.customer_user_id=NEW.customer_user_id AND m.taxpayer_id=NEW.taxpayer_id) THEN
     RAISE EXCEPTION 'Legacy Sandbox dispatch memory scope invalid';
   END IF;
   INSERT INTO invoice_dispatch_memory_scopes(id,legacy_sandbox_memory_id)
     VALUES(NEW.fiscal_memory_id,NEW.fiscal_memory_id) ON CONFLICT DO NOTHING;
   IF NOT EXISTS(SELECT 1 FROM invoice_dispatch_memory_scopes s WHERE s.id=NEW.fiscal_memory_id
     AND s.legacy_sandbox_memory_id=NEW.fiscal_memory_id) THEN
     RAISE EXCEPTION 'Dispatch memory UUID source collision';
   END IF;
 ELSE
   IF NOT EXISTS(SELECT 1 FROM fiscal_memories m JOIN taxpayer_profiles t ON t.id=m.taxpayer_id
     JOIN authority_invoice_fiscal_reservations r ON r.fiscal_memory_id=m.id
     WHERE m.id=NEW.fiscal_memory_id AND m.taxpayer_id=NEW.taxpayer_id AND m.status=2
       AND m.authority_environment=NEW.authority_environment
       AND t.customer_user_id=NEW.customer_user_id
       AND r.customer_user_id=NEW.customer_user_id AND r.taxpayer_id=NEW.taxpayer_id
       AND r.invoice_id=NEW.invoice_id AND r.invoice_revision=NEW.invoice_revision
       AND r.authority_environment=NEW.authority_environment) THEN
     RAISE EXCEPTION 'Classified dispatch memory and reservation scope invalid';
   END IF;
   INSERT INTO invoice_dispatch_memory_scopes(id,classified_fiscal_memory_id)
     VALUES(NEW.fiscal_memory_id,NEW.fiscal_memory_id) ON CONFLICT DO NOTHING;
   IF NOT EXISTS(SELECT 1 FROM invoice_dispatch_memory_scopes s WHERE s.id=NEW.fiscal_memory_id
     AND s.classified_fiscal_memory_id=NEW.fiscal_memory_id) THEN
     RAISE EXCEPTION 'Dispatch memory UUID source collision';
   END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER trg_dispatch_memory_source_immutable BEFORE UPDATE OR DELETE
 ON invoice_dispatch_memory_scopes FOR EACH ROW EXECUTE FUNCTION authority_dispatch_memory_scope_guard();
CREATE TRIGGER trg_authority_dispatch_memory_scope BEFORE INSERT
 ON invoice_dispatch_operations FOR EACH ROW EXECUTE FUNCTION authority_dispatch_memory_scope_guard();
""");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Production fiscal identity evidence is immutable; rollback requires separate review.");
}
