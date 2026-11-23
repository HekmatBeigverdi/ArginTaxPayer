using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCatalogOfficialMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE customer_catalog_items, catalog_official_mappings IN SHARE ROW EXCLUSIVE MODE;

                WITH codes AS (
                    SELECT m.item_id,
                           count(DISTINCT e.code) FILTER (
                               WHERE m.effective_from_utc <= now()
                                 AND (m.effective_to_utc IS NULL OR m.effective_to_utc > now())
                           ) AS active_count,
                           min(e.code) FILTER (
                               WHERE m.effective_from_utc <= now()
                                 AND (m.effective_to_utc IS NULL OR m.effective_to_utc > now())
                           ) AS active_code,
                           count(DISTINCT e.code) AS historical_count,
                           min(e.code) AS historical_code
                    FROM catalog_official_mappings m
                    JOIN official_goods_service_entries e ON e.id = m.official_entry_id
                    WHERE e.code ~ '^[0-9]{13}$'
                    GROUP BY m.item_id
                )
                UPDATE customer_catalog_items item
                SET official_code = CASE WHEN codes.active_count = 1 THEN codes.active_code
                                         ELSE codes.historical_code END
                FROM codes
                WHERE item.id = codes.item_id AND item.official_code IS NULL
                  AND (codes.active_count = 1 OR
                       (codes.active_count = 0 AND codes.historical_count = 1));

                DO $catalog$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM catalog_official_mappings m
                        JOIN customer_catalog_items item ON item.id = m.item_id
                        WHERE item.official_code IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Catalog migration stopped: a mapped item has no unambiguous 13-digit identifier';
                    END IF;
                END $catalog$;
                """);

            migrationBuilder.DropTable(name: "catalog_official_mappings");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Restore mapping history from the pre-migration backup.");
    }
}
