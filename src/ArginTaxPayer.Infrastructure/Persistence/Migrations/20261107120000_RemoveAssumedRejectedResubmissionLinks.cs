using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261107120000_RemoveAssumedRejectedResubmissionLinks")]
public sealed class RemoveAssumedRejectedResubmissionLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$ BEGIN
 IF NOT (
    current_database() = 'argintaxpayer_test'
    OR (current_database() = 'argin_phase18_acceptance'
        AND current_user = 'phase18_acceptance'
        AND EXISTS (SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
    OR (current_database() = 'argintaxpayer_prod'
        AND current_user = 'argin_prod_migrator'
        AND EXISTS (SELECT 1 FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl))
 ) THEN
   RAISE EXCEPTION 'Rejected-resubmission cleanup requires approved database identity';
 END IF;
END $$;
DROP TABLE IF EXISTS invoice_rejected_resubmission_links;
""");

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
