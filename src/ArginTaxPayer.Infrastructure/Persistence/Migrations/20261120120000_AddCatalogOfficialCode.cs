using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

public partial class AddCatalogOfficialCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE customer_catalog_items ADD COLUMN official_code varchar(13) NULL;
        ALTER TABLE customer_catalog_items ADD CONSTRAINT ck_catalog_item_official_code
          CHECK (official_code IS NULL OR official_code ~ '^[0-9]{13}$');
        DROP INDEX IF EXISTS ux_customer_catalog_internal_code;
        CREATE INDEX ix_customer_catalog_internal_code ON customer_catalog_items(customer_user_id, internal_code);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS ux_customer_catalog_internal_code;
            DROP INDEX IF EXISTS ix_customer_catalog_internal_code;
            CREATE INDEX ix_customer_catalog_internal_code ON customer_catalog_items(customer_user_id, internal_code);
            ALTER TABLE customer_catalog_items DROP CONSTRAINT IF EXISTS ck_catalog_item_official_code;
            ALTER TABLE customer_catalog_items DROP COLUMN official_code;
            """);
}
