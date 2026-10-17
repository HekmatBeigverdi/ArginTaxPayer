using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261017010000_AddInvoicePersistenceAndOperationClaims")]
public sealed class AddInvoicePersistenceAndOperationClaims : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // Additive, no backfill and no customer/production data access.
        m.Sql("""
CREATE TABLE invoice_documents (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 revision bigint NOT NULL,
 state integer NOT NULL,
 subject integer NOT NULL,
 issue_date_utc timestamptz NOT NULL,
 snapshot_json jsonb NOT NULL,
 snapshot_sha256 varchar(64) NOT NULL,
 updated_at_utc timestamptz NOT NULL,
 CONSTRAINT ck_invoice_documents_revision CHECK(revision > 0),
 CONSTRAINT ck_invoice_documents_state CHECK(state IN (1,2,3)),
 CONSTRAINT ck_invoice_documents_subject CHECK(subject IN (1,2,3,4)),
 CONSTRAINT ck_invoice_documents_hash CHECK(snapshot_sha256 ~ '^[0-9a-f]{64}$'));
CREATE UNIQUE INDEX ux_invoice_documents_scope ON invoice_documents(customer_user_id,taxpayer_id,id);
CREATE INDEX ix_invoice_documents_scope_date ON invoice_documents(customer_user_id,taxpayer_id,issue_date_utc);

CREATE TABLE invoice_operations (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 invoice_id uuid NOT NULL,
 idempotency_key varchar(128) NOT NULL,
 fingerprint_sha256 varchar(64) NOT NULL,
 expected_revision bigint NOT NULL,
 subject integer NOT NULL,
 reference_tax_id varchar(22) NULL,
 reference_slot integer NOT NULL,
 is_claim_active boolean NOT NULL,
 outcome_json jsonb NOT NULL,
 created_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_invoice_operations_scope FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id) REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ck_invoice_operations_revision CHECK(expected_revision > 0),
 CONSTRAINT ck_invoice_operations_subject CHECK(subject IN (1,2,3,4)),
 CONSTRAINT ck_invoice_operations_fingerprint CHECK(fingerprint_sha256 ~ '^[0-9a-f]{64}$'),
 CONSTRAINT ck_invoice_operations_slot CHECK(
 (subject=1 AND reference_tax_id IS NULL AND reference_slot=0)
 OR (subject IN (2,4) AND reference_tax_id ~ '^[A-Za-z0-9]{22}$' AND reference_slot=1)
 OR (subject=3 AND reference_tax_id ~ '^[A-Za-z0-9]{22}$' AND reference_slot=2)
 ));
CREATE UNIQUE INDEX ux_invoice_operations_idempotency
 ON invoice_operations(customer_user_id,taxpayer_id,idempotency_key);
CREATE UNIQUE INDEX ux_invoice_operations_active_reference
 ON invoice_operations(customer_user_id,taxpayer_id,reference_tax_id,reference_slot)
 WHERE is_claim_active AND reference_tax_id IS NOT NULL;

CREATE TABLE invoice_document_revisions (
 invoice_id uuid NOT NULL,
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 revision bigint NOT NULL,
 snapshot_json jsonb NOT NULL,
 snapshot_sha256 varchar(64) NOT NULL,
 captured_at_utc timestamptz NOT NULL,
 PRIMARY KEY(invoice_id,revision),
 CONSTRAINT fk_invoice_history_scope FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
 REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ck_invoice_history_revision CHECK(revision > 0),
 CONSTRAINT ck_invoice_history_hash CHECK(snapshot_sha256 ~ '^[0-9a-f]{64}
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 revision bigint NOT NULL,
 PRIMARY KEY(customer_user_id,taxpayer_id),
 CONSTRAINT ck_invoice_cursor_revision CHECK(revision >= 0));
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        // Refuse destruction of any historical invoice, claim or reservation.
        m.Sql("""
DO $$
BEGIN
 IF EXISTS(SELECT 1 FROM invoice_documents) OR
    EXISTS(SELECT 1 FROM invoice_operations) OR
    EXISTS(SELECT 1 FROM invoice_document_revisions) OR
    EXISTS(SELECT 1 FROM invoice_reservation_cursors) THEN
    RAISE EXCEPTION 'Invoice records exist: destructive rollback prohibited';
 END IF;
END $$;
DROP TABLE invoice_reservation_cursors;
DROP TRIGGER trg_phase07_invoice_history_guard ON invoice_document_revisions;
DROP FUNCTION phase07_invoice_history_guard();
DROP TABLE invoice_document_revisions;
DROP TABLE invoice_operations;
DROP TABLE invoice_documents;
""");
    }
}
));
CREATE INDEX ix_invoice_history_scope
 ON invoice_document_revisions(customer_user_id,taxpayer_id,invoice_id,revision);
CREATE FUNCTION phase07_invoice_history_guard() RETURNS trigger LANGUAGE plpgsql AS $
BEGIN
 RAISE EXCEPTION 'Committed invoice revision history is immutable';
END $;
CREATE TRIGGER trg_phase07_invoice_history_guard
 BEFORE UPDATE OR DELETE ON invoice_document_revisions
 FOR EACH ROW EXECUTE FUNCTION phase07_invoice_history_guard();

CREATE TABLE invoice_reservation_cursors (
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 revision bigint NOT NULL,
 PRIMARY KEY(customer_user_id,taxpayer_id),
 CONSTRAINT ck_invoice_cursor_revision CHECK(revision >= 0));
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        // Refuse destruction of any historical invoice, claim or reservation.
        m.Sql("""
DO $$
BEGIN
 IF EXISTS(SELECT 1 FROM invoice_documents) OR
    EXISTS(SELECT 1 FROM invoice_operations) OR
    EXISTS(SELECT 1 FROM invoice_reservation_cursors) THEN
    RAISE EXCEPTION 'Invoice records exist: destructive rollback prohibited';
 END IF;
END $$;
DROP TABLE invoice_reservation_cursors;
DROP TABLE invoice_operations;
DROP TABLE invoice_documents;
""");
    }
}
