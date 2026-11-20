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
        DO $$
        BEGIN
          IF EXISTS (
            SELECT m.item_id
            FROM catalog_official_mappings m
            JOIN official_goods_service_entries e ON e.id = m.official_entry_id
            WHERE m.effective_from_utc <= now()
              AND (m.effective_to_utc IS NULL OR m.effective_to_utc > now())
            GROUP BY m.item_id
            HAVING count(DISTINCT e.code) > 1
          ) THEN
            RAISE EXCEPTION 'Catalog official-code migration stopped: an item has multiple active identifiers';
          END IF;
        END $$;
        UPDATE customer_catalog_items item
        SET official_code = candidate.code
        FROM (
          SELECT DISTINCT ON (m.item_id) m.item_id, e.code
          FROM catalog_official_mappings m
          JOIN official_goods_service_entries e ON e.id = m.official_entry_id
          WHERE e.code ~ '^[0-9]{13}$'
          ORDER BY m.item_id,
            CASE WHEN m.effective_from_utc <= now() AND
              (m.effective_to_utc IS NULL OR m.effective_to_utc > now()) THEN 0 ELSE 1 END,
            m.effective_from_utc DESC, m.revision DESC, m.id
        ) candidate
        WHERE item.id = candidate.item_id;
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
