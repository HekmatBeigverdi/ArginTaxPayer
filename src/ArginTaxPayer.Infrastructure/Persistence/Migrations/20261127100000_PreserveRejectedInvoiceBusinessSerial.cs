using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261127100000_PreserveRejectedInvoiceBusinessSerial")]
public sealed class PreserveRejectedInvoiceBusinessSerial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
CREATE TABLE invoice_business_serial_retries (
 workspace_id uuid PRIMARY KEY,
 taxpayer_id uuid NOT NULL,
 persian_year smallint NOT NULL,
 serial bigint NOT NULL,
 FOREIGN KEY(taxpayer_id,persian_year,serial)
  REFERENCES invoice_business_serials(taxpayer_id,persian_year,serial)
);
CREATE OR REPLACE FUNCTION phase23_assign_business_invoice_serial()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE year_value smallint; serial_value bigint;
BEGIN
 IF EXISTS(SELECT 1 FROM invoice_business_serials WHERE workspace_id=NEW.id)
 OR EXISTS(SELECT 1 FROM invoice_business_serial_retries
           WHERE workspace_id=NEW.id AND taxpayer_id=NEW.taxpayer_id) THEN
  RETURN NEW;
 END IF;
 year_value:=phase08_persian_year_for_issue(NEW.issue_date_utc)::smallint;
 PERFORM pg_advisory_xact_lock(hashtextextended(NEW.taxpayer_id::text||':'||year_value::text,0));
 SELECT COALESCE(MAX(serial),0)+1 INTO serial_value FROM invoice_business_serials
  WHERE taxpayer_id=NEW.taxpayer_id AND persian_year=year_value;
 IF serial_value>9999999999 THEN RAISE EXCEPTION 'Business invoice serial sequence exhausted'; END IF;
 INSERT INTO invoice_business_serials(taxpayer_id,persian_year,serial,workspace_id)
  VALUES(NEW.taxpayer_id,year_value,serial_value,NEW.id);
 RETURN NEW;
END $$;
""");

    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException(
        "Removing retry serial links would lose invoice numbering history.");
}
