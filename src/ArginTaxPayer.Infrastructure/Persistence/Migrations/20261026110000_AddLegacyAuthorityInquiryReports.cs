using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261026110000_AddLegacyAuthorityInquiryReports")]
public sealed class AddLegacyAuthorityInquiryReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
DO $$ BEGIN
 IF current_database()<>'argintaxpayer_test' THEN
  RAISE EXCEPTION 'Legacy Sandbox reports require argintaxpayer_test';
 END IF;
END $$;
CREATE TABLE sandbox_authority_inquiry_reports (
 id uuid PRIMARY KEY,
 operation_id uuid NOT NULL REFERENCES invoice_dispatch_operations(id),
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 invoice_id uuid NOT NULL,
 authority_reference varchar(128) NOT NULL,
 report_json jsonb NOT NULL,
 observed_at_utc timestamptz NOT NULL,
 FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
  REFERENCES invoice_documents(customer_user_id,taxpayer_id,id)
);
CREATE INDEX ix_sandbox_authority_reports_invoice
 ON sandbox_authority_inquiry_reports(customer_user_id,taxpayer_id,invoice_id,observed_at_utc DESC);
CREATE TABLE sandbox_invoice_correction_drafts (
 operation_id uuid PRIMARY KEY REFERENCES invoice_dispatch_operations(id),
 report_id uuid NOT NULL REFERENCES sandbox_authority_inquiry_reports(id),
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 invoice_id uuid NOT NULL,
 workspace_id uuid NOT NULL UNIQUE REFERENCES invoice_entry_workspaces(id),
 actor_user_id uuid NOT NULL REFERENCES identity_users(id),
 created_at_utc timestamptz NOT NULL,
 FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
  REFERENCES invoice_documents(customer_user_id,taxpayer_id,id)
);
CREATE FUNCTION sandbox_authority_reports_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Authority reports and correction links are append-only'; END $$;
CREATE TRIGGER trg_sandbox_authority_reports_immutable BEFORE UPDATE OR DELETE
 ON sandbox_authority_inquiry_reports FOR EACH ROW EXECUTE FUNCTION sandbox_authority_reports_immutable();
CREATE TRIGGER trg_sandbox_correction_drafts_immutable BEFORE UPDATE OR DELETE
 ON sandbox_invoice_correction_drafts FOR EACH ROW EXECUTE FUNCTION sandbox_authority_reports_immutable();
""");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Authority history must be retained.");
}
