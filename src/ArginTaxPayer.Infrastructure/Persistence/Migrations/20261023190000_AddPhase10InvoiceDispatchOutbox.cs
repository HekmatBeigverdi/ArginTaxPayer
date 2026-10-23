using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Additive-only Phase10 dispatch store. NEVER automatically applied by hosts.
/// Applies exclusively to the explicitly named isolated test database.
/// SQL-level schema is intentionally not exposed as EF tracked entities: the
/// repository uses transactional commands, avoiding a premature model snapshot
/// mutation while the eventual production migration is separately reviewed.
/// </summary>
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261023190000_AddPhase10InvoiceDispatchOutbox")]
public sealed class AddPhase10InvoiceDispatchOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
BEGIN
 IF current_database() <> 'argintaxpayer_test' THEN
   RAISE EXCEPTION 'Phase10 dispatch migration is test-db-only (argintaxpayer_test)';
 END IF;
END $$;

CREATE TABLE invoice_dispatch_operations (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 fiscal_memory_id uuid NOT NULL REFERENCES fiscal_memories(id) ON DELETE RESTRICT,
 invoice_id uuid NOT NULL,
 invoice_revision bigint NOT NULL,
 idempotency_key varchar(128) NOT NULL,
 payload_sha256 varchar(64) NOT NULL,
 state integer NOT NULL DEFAULT 1,
 state_revision bigint NOT NULL DEFAULT 1,
 rule_set_edition varchar(128) NOT NULL,
 mapper_version varchar(80) NOT NULL,
 signing_profile varchar(128) NOT NULL,
 created_at_utc timestamptz NOT NULL,
 updated_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_p10_dispatch_invoice FOREIGN KEY
  (customer_user_id,taxpayer_id,invoice_id)
  REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ck_p10_dispatch_revision CHECK(invoice_revision > 0 AND state_revision > 0),
 CONSTRAINT ck_p10_dispatch_state CHECK(state BETWEEN 1 AND 8),
 CONSTRAINT ck_p10_dispatch_hash CHECK(payload_sha256 ~ '^[0-9a-f]{64}$'),
 CONSTRAINT ck_p10_dispatch_key CHECK(length(idempotency_key) BETWEEN 1 AND 128),
 CONSTRAINT ck_p10_dispatch_rule CHECK(length(rule_set_edition)>0 AND length(mapper_version)>0 AND length(signing_profile)>0)
);
CREATE UNIQUE INDEX ux_p10_dispatch_key
 ON invoice_dispatch_operations(customer_user_id,taxpayer_id,idempotency_key);
CREATE UNIQUE INDEX ux_p10_dispatch_invoice_revision
 ON invoice_dispatch_operations(customer_user_id,taxpayer_id,fiscal_memory_id,invoice_id,invoice_revision);
CREATE INDEX ix_p10_dispatch_pending
 ON invoice_dispatch_operations(state,created_at_utc)
 WHERE state IN (3,4,6,7);

CREATE TABLE invoice_dispatch_attempts (
 id uuid PRIMARY KEY,
 operation_id uuid NOT NULL REFERENCES invoice_dispatch_operations(id) ON DELETE RESTRICT,
 attempt_number integer NOT NULL CHECK(attempt_number>0),
 endpoint_code varchar(80) NOT NULL CHECK(endpoint_code='tax-authority-submit'),
 started_at_utc timestamptz NOT NULL,
 ended_at_utc timestamptz NULL,
 result_code varchar(100) NULL,
 UNIQUE(operation_id,attempt_number),
 CONSTRAINT ck_p10_attempt_chronology CHECK(ended_at_utc IS NULL OR ended_at_utc>=started_at_utc)
);
CREATE INDEX ix_p10_attempts_operation ON invoice_dispatch_attempts(operation_id,attempt_number);

CREATE TABLE invoice_dispatch_receipts (
 id uuid PRIMARY KEY,
 operation_id uuid NOT NULL REFERENCES invoice_dispatch_operations(id) ON DELETE RESTRICT,
 attempt_id uuid NULL,
 receipt_kind integer NOT NULL CHECK(receipt_kind IN (1,2)),
 authority_reference varchar(128) NULL,
 evidence_sha256 varchar(64) NOT NULL CHECK(evidence_sha256 ~ '^[0-9a-f]{64}$'),
 observed_at_utc timestamptz NOT NULL,
 CONSTRAINT ck_p10_receipt_reference CHECK(authority_reference IS NULL OR length(authority_reference)>0)
);
CREATE UNIQUE INDEX ux_p10_attempt_owner ON invoice_dispatch_attempts(operation_id,id);
ALTER TABLE invoice_dispatch_receipts ADD CONSTRAINT fk_p10_receipt_same_operation
 FOREIGN KEY(operation_id,attempt_id)
 REFERENCES invoice_dispatch_attempts(operation_id,id) ON DELETE RESTRICT;
CREATE INDEX ix_p10_receipts_operation ON invoice_dispatch_receipts(operation_id,observed_at_utc);

CREATE FUNCTION phase10_append_only_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Phase10 dispatch evidence is append-only';
END $$;
CREATE TRIGGER trg_p10_attempts_immutable BEFORE UPDATE OR DELETE
 ON invoice_dispatch_attempts FOR EACH ROW EXECUTE FUNCTION phase10_append_only_guard();
CREATE TRIGGER trg_p10_receipts_immutable BEFORE UPDATE OR DELETE
 ON invoice_dispatch_receipts FOR EACH ROW EXECUTE FUNCTION phase10_append_only_guard();
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Invoice submission evidence must not be destroyed by rollback.
        throw new NotSupportedException(
            "Phase10 dispatch records require a separately approved retention-preserving rollback.");
    }
}
