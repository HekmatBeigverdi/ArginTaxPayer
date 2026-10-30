using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261030210000_AddSandboxSalesReturnWorkspaces")]
public sealed class AddSandboxSalesReturnWorkspaces : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE sandbox_invoice_sales_return_workspaces (
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
          CONSTRAINT fk_sales_return_predecessor FOREIGN KEY
           (customer_user_id,taxpayer_id,predecessor_invoice_id)
           REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
          CONSTRAINT ck_sales_return_revision CHECK(revision > 0),
          CONSTRAINT ck_sales_return_state CHECK(state IN (1,2,3)),
          CONSTRAINT ck_sales_return_taxid CHECK(predecessor_fiscal_tax_id ~ '^[A-Za-z0-9]{22}$')
        );
        CREATE UNIQUE INDEX ux_sales_return_active_draft
        ON sandbox_invoice_sales_return_workspaces(customer_user_id,taxpayer_id,predecessor_invoice_id)
        WHERE state=1;
        CREATE INDEX ix_sales_return_workspace_owner
        ON sandbox_invoice_sales_return_workspaces(customer_user_id,taxpayer_id,updated_at_utc);
        CREATE INDEX ix_sales_return_legal_relations
        ON sandbox_invoice_legal_relations(customer_user_id,taxpayer_id,predecessor_invoice_id,subject,state)
        WHERE subject=4;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX IF EXISTS ix_sales_return_legal_relations;
        DROP TABLE IF EXISTS sandbox_invoice_sales_return_workspaces;
        """);
}
