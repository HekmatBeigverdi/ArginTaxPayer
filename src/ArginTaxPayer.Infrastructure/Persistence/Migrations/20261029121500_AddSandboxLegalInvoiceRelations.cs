using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261029121500_AddSandboxLegalInvoiceRelations")]
public sealed class AddSandboxLegalInvoiceRelations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE sandbox_invoice_legal_relations (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 predecessor_invoice_id uuid NOT NULL,
 successor_invoice_id uuid NOT NULL,
 predecessor_fiscal_tax_id varchar(22) NOT NULL,
 subject integer NOT NULL,
 state integer NOT NULL DEFAULT 1,
 idempotency_key varchar(128) NOT NULL,
 evidence_reference varchar(128) NOT NULL,
 created_at_utc timestamptz NOT NULL,
 confirmed_at_utc timestamptz NULL,
 CONSTRAINT fk_sandbox_legal_predecessor FOREIGN KEY
 (customer_user_id,taxpayer_id,predecessor_invoice_id)
 REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT fk_sandbox_legal_successor FOREIGN KEY
 (customer_user_id,taxpayer_id,successor_invoice_id)
 REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ck_sandbox_legal_relation_subject CHECK(subject IN (2,3,4)),
 CONSTRAINT ck_sandbox_legal_relation_state CHECK(state IN (1,2,3,4)),
 CONSTRAINT ck_sandbox_legal_relation_distinct CHECK(predecessor_invoice_id<>successor_invoice_id),
 CONSTRAINT ck_sandbox_legal_relation_reference CHECK(predecessor_fiscal_tax_id ~ '^[A-Za-z0-9]{22}$'),
 CONSTRAINT ck_sandbox_legal_relation_evidence CHECK(length(trim(evidence_reference))>0)
);
CREATE UNIQUE INDEX ux_sandbox_legal_successor
 ON sandbox_invoice_legal_relations(customer_user_id,taxpayer_id,successor_invoice_id);
CREATE UNIQUE INDEX ux_sandbox_legal_idempotency
 ON sandbox_invoice_legal_relations(customer_user_id,taxpayer_id,idempotency_key);
CREATE UNIQUE INDEX ux_sandbox_legal_active_cancellation
 ON sandbox_invoice_legal_relations(customer_user_id,taxpayer_id,predecessor_invoice_id)
 WHERE subject=3 AND state IN (1,2,4);
CREATE INDEX ix_sandbox_legal_predecessor
 ON sandbox_invoice_legal_relations(customer_user_id,taxpayer_id,predecessor_invoice_id,created_at_utc);
""");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS sandbox_invoice_legal_relations;");
    }
}
