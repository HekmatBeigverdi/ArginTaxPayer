using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261018150000_AddResumableInvoiceEntryWorkspaces")]
public sealed class AddResumableInvoiceEntryWorkspaces : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Draft-only workspace: no changes to fiscal invoice history or issued status.
        migrationBuilder.Sql("""
CREATE TABLE invoice_entry_workspaces(
 id uuid NOT NULL PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 revision bigint NOT NULL,
 last_completed_step integer NOT NULL,
 issue_date_utc timestamptz NOT NULL,
 draft_json jsonb NOT NULL,
 updated_at_utc timestamptz NOT NULL,
 CONSTRAINT ck_invoice_entry_workspace_revision CHECK (revision > 0),
 CONSTRAINT ck_invoice_entry_workspace_step CHECK (last_completed_step IN (0,1,2))
);
CREATE INDEX ix_invoice_entry_workspaces_scope
 ON invoice_entry_workspaces(customer_user_id,taxpayer_id,updated_at_utc);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DO $$
BEGIN
 IF EXISTS(SELECT 1 FROM invoice_entry_workspaces) THEN
  RAISE EXCEPTION 'Resumable invoice drafts exist; destructive rollback denied';
 END IF;
END $$;
DROP TABLE invoice_entry_workspaces;
""");
    }
}