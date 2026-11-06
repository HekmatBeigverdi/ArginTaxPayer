using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261105040000_AddProductionAuthorityInquiryReports")]
public sealed class AddProductionAuthorityInquiryReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$ BEGIN
 IF NOT (
   current_database()='argintaxpayer_test'
   OR (current_database()='argin_phase18_acceptance' AND current_user='phase18_acceptance'
       AND EXISTS(SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
   OR (current_database()='argintaxpayer_prod' AND current_user='argin_prod_migrator'
       AND EXISTS(SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
 ) THEN RAISE EXCEPTION 'Production authority reports require approved DB identity'; END IF;
END $$;
CREATE TABLE production_authority_inquiry_reports (
 id uuid PRIMARY KEY,
 operation_id uuid NOT NULL REFERENCES invoice_dispatch_operations(id) ON DELETE RESTRICT,
 customer_user_id uuid NOT NULL,
 taxpayer_id uuid NOT NULL,
 invoice_id uuid NOT NULL,
 authority_reference varchar(128) NOT NULL,
 report_json jsonb NOT NULL,
 observed_at_utc timestamptz NOT NULL,
 FOREIGN KEY(customer_user_id,taxpayer_id,invoice_id)
  REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT
);
CREATE INDEX ix_production_authority_reports_invoice
 ON production_authority_inquiry_reports(customer_user_id,taxpayer_id,invoice_id,observed_at_utc DESC);
CREATE FUNCTION production_authority_reports_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Production authority reports are append-only'; END $$;
CREATE TRIGGER trg_production_authority_reports_immutable BEFORE UPDATE OR DELETE
 ON production_authority_inquiry_reports FOR EACH ROW EXECUTE FUNCTION production_authority_reports_immutable();
""");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Production authority inquiry history must be retained.");
}
