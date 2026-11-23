using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261123100000_AddInvoiceBusinessSerials")]
public sealed class AddInvoiceBusinessSerials : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE invoice_business_serials (
 taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
 persian_year smallint NOT NULL,
 serial bigint NOT NULL,
 workspace_id uuid NOT NULL,
 assigned_at_utc timestamptz NOT NULL DEFAULT now(),
 CONSTRAINT pk_invoice_business_serials PRIMARY KEY(taxpayer_id,persian_year,serial),
 CONSTRAINT uq_invoice_business_serial_workspace UNIQUE(workspace_id),
 CONSTRAINT ck_invoice_business_serial_range CHECK(serial BETWEEN 1 AND 9999999999)
);
CREATE INDEX ix_invoice_business_serial_workspace ON invoice_business_serials(workspace_id);
WITH source_rows AS (
 SELECT DISTINCT ON (id) id,taxpayer_id,issue_date_utc
 FROM (
  SELECT id,taxpayer_id,issue_date_utc,0 AS priority FROM invoice_documents
  UNION ALL
  SELECT id,taxpayer_id,issue_date_utc,1 AS priority FROM invoice_entry_workspaces
 ) all_rows
 ORDER BY id,priority
), numbered AS (
 SELECT id,taxpayer_id,issue_date_utc,
  phase08_persian_year_for_issue(issue_date_utc)::smallint AS persian_year,
  row_number() OVER(PARTITION BY taxpayer_id,
   phase08_persian_year_for_issue(issue_date_utc) ORDER BY issue_date_utc,id) AS serial
 FROM source_rows
)
INSERT INTO invoice_business_serials(taxpayer_id,persian_year,serial,workspace_id)
SELECT taxpayer_id,persian_year,serial,id FROM numbered
WHERE serial<=9999999999;
UPDATE invoice_entry_workspaces w
SET draft_json=jsonb_set(w.draft_json,'{businessInvoiceSerial}',to_jsonb(s.serial),true)
FROM invoice_business_serials s
WHERE s.workspace_id=w.id AND NOT (w.draft_json ? 'businessInvoiceSerial');
CREATE FUNCTION phase23_assign_business_invoice_serial()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE year_value smallint; serial_value bigint;
BEGIN
 year_value:=phase08_persian_year_for_issue(NEW.issue_date_utc)::smallint;
 IF EXISTS(SELECT 1 FROM invoice_business_serials WHERE workspace_id=NEW.id) THEN
  RETURN NEW;
 END IF;
 PERFORM pg_advisory_xact_lock(hashtextextended(NEW.taxpayer_id::text||':'||year_value::text,0));
 SELECT COALESCE(MAX(serial),0)+1 INTO serial_value FROM invoice_business_serials
  WHERE taxpayer_id=NEW.taxpayer_id AND persian_year=year_value;
 IF serial_value>9999999999 THEN RAISE EXCEPTION 'Business invoice serial sequence exhausted'; END IF;
 INSERT INTO invoice_business_serials(taxpayer_id,persian_year,serial,workspace_id)
  VALUES(NEW.taxpayer_id,year_value,serial_value,NEW.id);
 RETURN NEW;
END $$;
CREATE TRIGGER trg_invoice_workspace_business_serial
 BEFORE INSERT ON invoice_entry_workspaces FOR EACH ROW
 EXECUTE FUNCTION phase23_assign_business_invoice_serial();
CREATE TRIGGER trg_invoice_document_business_serial
 BEFORE INSERT ON invoice_documents FOR EACH ROW
 EXECUTE FUNCTION phase23_assign_business_invoice_serial();
""");

    protected override void Down(MigrationBuilder m) => m.Sql("""
DROP TRIGGER IF EXISTS trg_invoice_document_business_serial ON invoice_documents;
DROP TRIGGER IF EXISTS trg_invoice_workspace_business_serial ON invoice_entry_workspaces;
DROP FUNCTION IF EXISTS phase23_assign_business_invoice_serial();
DROP TABLE invoice_business_serials;
""");
}
