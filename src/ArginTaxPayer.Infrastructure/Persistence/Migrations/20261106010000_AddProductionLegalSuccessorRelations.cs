using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261106010000_AddProductionLegalSuccessorRelations")]
public sealed class AddProductionLegalSuccessorRelations : Migration
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
   RAISE EXCEPTION 'Production legal-successor relations require approved database identity';
 END IF;
END $$;

CREATE TABLE authority_invoice_legal_relations (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 predecessor_invoice_id uuid NOT NULL,
 successor_invoice_id uuid NOT NULL,
 predecessor_fiscal_tax_id varchar(22) NOT NULL CHECK(predecessor_fiscal_tax_id~'^[A-Z0-9]{22}$'),
 predecessor_operation_id uuid NOT NULL REFERENCES invoice_dispatch_operations(id) ON DELETE RESTRICT,
 subject integer NOT NULL CHECK(subject IN (2,3,4)),
 state integer NOT NULL CHECK(state IN (1,2,3,4)),
 idempotency_key varchar(160) NOT NULL,
 evidence_reference varchar(256) NOT NULL,
 created_at_utc timestamptz NOT NULL,
 confirmed_at_utc timestamptz NULL,
 CONSTRAINT fk_authority_legal_predecessor FOREIGN KEY(customer_user_id,taxpayer_id,predecessor_invoice_id)
   REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT fk_authority_legal_successor FOREIGN KEY(customer_user_id,taxpayer_id,successor_invoice_id)
   REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ux_authority_legal_successor UNIQUE(customer_user_id,taxpayer_id,successor_invoice_id),
 CONSTRAINT ux_authority_legal_idempotency UNIQUE(customer_user_id,taxpayer_id,idempotency_key)
);
CREATE UNIQUE INDEX ux_authority_legal_cancel_claim
 ON authority_invoice_legal_relations(customer_user_id,taxpayer_id,predecessor_invoice_id)
 WHERE subject=3 AND state IN (1,2,4);
CREATE UNIQUE INDEX ux_authority_legal_correction_return_claim
 ON authority_invoice_legal_relations(customer_user_id,taxpayer_id,predecessor_invoice_id)
 WHERE subject IN (2,4) AND state IN (1,2,4);

CREATE TABLE authority_invoice_correction_workspaces (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 predecessor_invoice_id uuid NOT NULL,
 predecessor_fiscal_tax_id varchar(22) NOT NULL CHECK(predecessor_fiscal_tax_id~'^[A-Z0-9]{22}$'),
 revision bigint NOT NULL CHECK(revision>0),
 draft_json jsonb NOT NULL,
 state integer NOT NULL CHECK(state IN (1,2,3)),
 created_at_utc timestamptz NOT NULL,
 updated_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_authority_correction_predecessor FOREIGN KEY(customer_user_id,taxpayer_id,predecessor_invoice_id)
   REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX ux_authority_correction_active_workspace
 ON authority_invoice_correction_workspaces(customer_user_id,taxpayer_id,predecessor_invoice_id) WHERE state=1;

CREATE TABLE authority_invoice_sales_return_workspaces (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 predecessor_invoice_id uuid NOT NULL,
 predecessor_fiscal_tax_id varchar(22) NOT NULL CHECK(predecessor_fiscal_tax_id~'^[A-Z0-9]{22}$'),
 revision bigint NOT NULL CHECK(revision>0),
 draft_json jsonb NOT NULL,
 state integer NOT NULL CHECK(state IN (1,2,3)),
 created_at_utc timestamptz NOT NULL,
 updated_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_authority_sales_return_predecessor FOREIGN KEY(customer_user_id,taxpayer_id,predecessor_invoice_id)
   REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX ux_authority_sales_return_active_workspace
 ON authority_invoice_sales_return_workspaces(customer_user_id,taxpayer_id,predecessor_invoice_id) WHERE state=1;

CREATE OR REPLACE FUNCTION authority_fiscal_reservation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
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
     AND i.state=3 AND i.revision=NEW.invoice_revision
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
     AND (i.subject=1 OR EXISTS (
       SELECT 1 FROM authority_invoice_legal_relations lr
       JOIN authority_invoice_fiscal_reservations pr ON pr.customer_user_id=lr.customer_user_id
         AND pr.taxpayer_id=lr.taxpayer_id AND pr.invoice_id=lr.predecessor_invoice_id
         AND pr.fiscal_memory_id=NEW.fiscal_memory_id AND pr.authority_environment=1
       JOIN production_authority_inquiry_reports ir ON ir.operation_id=lr.predecessor_operation_id
         AND ir.customer_user_id=lr.customer_user_id AND ir.taxpayer_id=lr.taxpayer_id
         AND ir.invoice_id=lr.predecessor_invoice_id
       WHERE lr.customer_user_id=i.customer_user_id AND lr.taxpayer_id=i.taxpayer_id
         AND lr.successor_invoice_id=i.id AND lr.subject=i.subject
         AND NEW.authority_environment=1 AND lr.state IN (1,2,4)
         AND lr.predecessor_fiscal_tax_id=i.snapshot_json::jsonb->>'referenceTaxId'
         AND ir.report_json->>'Status'='SUCCESS'
     ))
 ) THEN RAISE EXCEPTION 'Production fiscal reservation scope is not verified'; END IF;
 RETURN NEW;
END $$;
""");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Production legal lineage is immutable evidence; rollback requires separate review.");
}
