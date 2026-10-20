using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261020104500_AddInvoiceWorkspaceRevisionAudit")]
public sealed class AddInvoiceWorkspaceRevisionAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE invoice_entry_workspace_revisions(
 workspace_id uuid NOT NULL REFERENCES invoice_entry_workspaces(id) ON DELETE RESTRICT,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 revision bigint NOT NULL CHECK (revision > 0),
 actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 action varchar(16) NOT NULL CHECK(action IN ('create','save')),
 reason varchar(1000) NOT NULL CHECK(length(trim(reason)) > 0),
 snapshot_json jsonb NOT NULL,
 occurred_at_utc timestamptz NOT NULL,
 PRIMARY KEY(workspace_id, revision)
);
CREATE INDEX ix_invoice_workspace_history_scope
 ON invoice_entry_workspace_revisions(customer_user_id,taxpayer_id,workspace_id,revision);
CREATE FUNCTION phase08_workspace_history_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Invoice workspace history is append-only';
END $$;
CREATE TRIGGER trg_phase08_workspace_history_immutable
 BEFORE UPDATE OR DELETE ON invoice_entry_workspace_revisions
 FOR EACH ROW EXECUTE FUNCTION phase08_workspace_history_immutable();
""");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DO $$
BEGIN
 IF EXISTS(SELECT 1 FROM invoice_entry_workspace_revisions) THEN
  RAISE EXCEPTION 'Cannot remove nonempty invoice workspace audit history';
 END IF;
END $$;
DROP TRIGGER trg_phase08_workspace_history_immutable ON invoice_entry_workspace_revisions;
DROP FUNCTION phase08_workspace_history_immutable();
DROP TABLE invoice_entry_workspace_revisions;
""");
    }
}
