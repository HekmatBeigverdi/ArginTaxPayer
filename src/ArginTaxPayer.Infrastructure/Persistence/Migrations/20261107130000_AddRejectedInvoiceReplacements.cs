using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261107130000_AddRejectedInvoiceReplacements")]
public sealed class AddRejectedInvoiceReplacements : Migration
{
    internal const string SuccessfulReplacementsViewSql = """
CREATE VIEW invoice_successful_rejection_replacements AS
SELECT DISTINCT ON (r.customer_user_id,r.taxpayer_id,r.predecessor_invoice_id)
 r.customer_user_id,r.taxpayer_id,r.predecessor_invoice_id,r.successor_invoice_id
FROM invoice_rejection_replacements r
JOIN invoice_documents source ON source.id=r.predecessor_invoice_id
 AND source.customer_user_id=r.customer_user_id AND source.taxpayer_id=r.taxpayer_id
 AND source.subject=1
JOIN invoice_documents replacement ON replacement.id=r.successor_invoice_id
 AND replacement.customer_user_id=r.customer_user_id AND replacement.taxpayer_id=r.taxpayer_id
 AND replacement.subject=1
JOIN invoice_dispatch_operations source_operation ON source_operation.id=r.predecessor_operation_id
 AND source_operation.invoice_id=source.id AND source_operation.customer_user_id=r.customer_user_id
 AND source_operation.taxpayer_id=r.taxpayer_id
JOIN LATERAL (
 SELECT report_json->>'Status' AS status FROM production_authority_inquiry_reports
 WHERE operation_id=source_operation.id AND invoice_id=source.id
  AND customer_user_id=r.customer_user_id AND taxpayer_id=r.taxpayer_id
 ORDER BY observed_at_utc DESC,id DESC LIMIT 1
) rejection ON rejection.status='FAILED'
JOIN LATERAL (
 SELECT report.report_json->>'Status' AS status
 FROM production_authority_inquiry_reports report
 JOIN invoice_dispatch_operations operation ON operation.id=report.operation_id
  AND operation.invoice_id=report.invoice_id AND operation.customer_user_id=report.customer_user_id
  AND operation.taxpayer_id=report.taxpayer_id
 WHERE report.invoice_id=replacement.id AND report.customer_user_id=r.customer_user_id
  AND report.taxpayer_id=r.taxpayer_id
 ORDER BY operation.invoice_revision DESC,operation.created_at_utc DESC,
  report.observed_at_utc DESC,report.id DESC LIMIT 1
) accepted ON accepted.status='SUCCESS'
ORDER BY r.customer_user_id,r.taxpayer_id,r.predecessor_invoice_id,r.created_at_utc DESC,r.id DESC;
""";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$ BEGIN
 IF NOT (
    current_database()='argintaxpayer_test'
    OR (current_database()='argin_phase18_acceptance' AND current_user='phase18_acceptance'
        AND EXISTS(SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
    OR (current_database()='argintaxpayer_prod' AND current_user='argin_prod_migrator'
        AND EXISTS(SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
 ) THEN RAISE EXCEPTION 'Invoice replacement migration requires approved database identity';
 END IF;
END $$;
CREATE TABLE invoice_rejection_replacements (
 id uuid PRIMARY KEY,
 customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 predecessor_invoice_id uuid NOT NULL,
 predecessor_operation_id uuid NOT NULL REFERENCES invoice_dispatch_operations(id) ON DELETE RESTRICT,
 successor_invoice_id uuid NOT NULL,
 created_at_utc timestamptz NOT NULL,
 CONSTRAINT fk_invoice_replacement_predecessor
  FOREIGN KEY(customer_user_id,taxpayer_id,predecessor_invoice_id)
  REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
 CONSTRAINT ux_invoice_replacement_successor UNIQUE(customer_user_id,taxpayer_id,successor_invoice_id),
 CONSTRAINT ck_invoice_replacement_distinct CHECK(predecessor_invoice_id<>successor_invoice_id)
);
CREATE INDEX ix_invoice_replacement_predecessor
 ON invoice_rejection_replacements(customer_user_id,taxpayer_id,predecessor_invoice_id,created_at_utc);
""");
        migrationBuilder.Sql(SuccessfulReplacementsViewSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
DROP VIEW invoice_successful_rejection_replacements;
DROP TABLE invoice_rejection_replacements;
""");
}
