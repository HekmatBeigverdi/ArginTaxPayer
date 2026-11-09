using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261109010000_AllowPartialSalesReturns")]
public sealed class AllowPartialSalesReturns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
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
            RAISE EXCEPTION 'Legal successor index migration requires approved database identity';
          END IF;
        END $$;
        DROP INDEX IF EXISTS ux_authority_legal_correction_return_claim;
        CREATE UNIQUE INDEX ux_authority_legal_correction_claim
          ON authority_invoice_legal_relations(customer_user_id,taxpayer_id,predecessor_invoice_id)
          WHERE subject=2 AND state IN (1,2,4);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX IF EXISTS ux_authority_legal_correction_claim;
        CREATE UNIQUE INDEX ux_authority_legal_correction_return_claim
          ON authority_invoice_legal_relations(customer_user_id,taxpayer_id,predecessor_invoice_id)
          WHERE subject IN (2,4) AND state IN (1,2,4);
        """);
}
