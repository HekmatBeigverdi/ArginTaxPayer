using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261030140000_AddSandboxCorrectionWorkspaces")]
public sealed class AddSandboxCorrectionWorkspaces : Migration
{
 protected override void Up(MigrationBuilder m)=>m.Sql("""
CREATE TABLE sandbox_invoice_correction_workspaces (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 predecessor_invoice_id uuid NOT NULL,
 predecessor_fiscal_tax_id varchar(22) NOT NULL,
 revision bigint NOT NULL DEFAULT 1,
 draft_json jsonb NOT NULL,
 state integer NOT NULL DEFAULT 1,
 created_at_utc timestamptz NOT NULL,
 updated_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_correction_predecessor FOREIGN KEY
 (customer_user_id,taxpayer_id,predecessor_invoice_id)
 REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ck_correction_workspace_revision CHECK(revision>0),
 CONSTRAINT ck_correction_workspace_state CHECK(state IN (1,2,3)),
 CONSTRAINT ck_correction_workspace_taxid CHECK(predecessor_fiscal_tax_id ~ '^[A-Za-z0-9]{22}$')
);
CREATE UNIQUE INDEX ux_correction_active_predecessor
 ON sandbox_invoice_correction_workspaces(customer_user_id,taxpayer_id,predecessor_invoice_id)
 WHERE state IN (1,2);
CREATE INDEX ix_correction_workspace_owner
 ON sandbox_invoice_correction_workspaces(customer_user_id,taxpayer_id,updated_at_utc);
""");
 protected override void Down(MigrationBuilder m)=>m.Sql(
 "DROP TABLE IF EXISTS sandbox_invoice_correction_workspaces;");
}
