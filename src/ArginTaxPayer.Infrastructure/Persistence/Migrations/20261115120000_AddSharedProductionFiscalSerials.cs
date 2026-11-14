using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261115120000_AddSharedProductionFiscalSerials")]
public sealed class AddSharedProductionFiscalSerials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
CREATE TABLE production_shared_serial_floors (
 memory_code varchar(6) PRIMARY KEY CHECK(memory_code ~ '^[A-Z0-9]{6}$'),
 last_serial bigint NOT NULL CHECK(last_serial BETWEEN 0 AND 1099511627775)
);
CREATE TABLE production_shared_serial_reservations (
 memory_code varchar(6) NOT NULL CHECK(memory_code ~ '^[A-Z0-9]{6}$'),
 serial bigint NOT NULL CHECK(serial BETWEEN 1 AND 1099511627775),
 invoice_id uuid NOT NULL,
 customer_user_id uuid NOT NULL,
 snapshot_sha256 varchar(64) NOT NULL,
 issue_date_utc timestamptz NOT NULL,
 created_at_utc timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(memory_code,serial),
 UNIQUE(memory_code,customer_user_id,invoice_id)
);
CREATE FUNCTION production_shared_serial_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Shared Production serial reservations are immutable'; END $$;
CREATE TRIGGER production_shared_serial_no_mutation BEFORE UPDATE OR DELETE
 ON production_shared_serial_reservations FOR EACH ROW EXECUTE FUNCTION production_shared_serial_immutable();
DO $$ BEGIN
 IF current_database()='argintaxpayer_prod' AND EXISTS(SELECT 1 FROM pg_roles WHERE rolname='argin_prod_app') THEN
  GRANT SELECT,INSERT ON production_shared_serial_reservations TO argin_prod_app;
  GRANT SELECT ON production_shared_serial_floors TO argin_prod_app;
 END IF;
END $$;
""");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Shared fiscal reservation evidence must be retained.");
}
