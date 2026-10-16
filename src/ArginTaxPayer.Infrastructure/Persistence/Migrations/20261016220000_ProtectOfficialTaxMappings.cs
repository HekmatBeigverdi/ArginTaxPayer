using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261016220000_ProtectOfficialTaxMappings")]
public sealed class ProtectOfficialTaxMappings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE FUNCTION phase06_tax_mapping_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
  item_kind integer;
  official_kind integer;
  verification integer;
  release_state integer;
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'Historical official tax mappings cannot be deleted';
  END IF;
  IF TG_OP = 'UPDATE' THEN
    RAISE EXCEPTION 'Historical tax mapping snapshots cannot be updated; create another dated row';
  END IF;
  -- Serialize all inserts for the same item, including different official entries.
  SELECT kind INTO item_kind FROM customer_catalog_items
    WHERE id = NEW.item_id FOR UPDATE;
  SELECT e.kind, e.verification_status, r.state
    INTO official_kind, verification, release_state
    FROM official_goods_service_entries e
    JOIN catalog_reference_releases r ON r.id = e.release_id
    WHERE e.id = NEW.official_entry_id;
  IF item_kind IS NULL OR official_kind IS NULL OR verification <> 2 OR release_state <> 3 THEN
    RAISE EXCEPTION 'Official identifier is missing, unverified or not active';
  END IF;
  IF NOT ((item_kind IN (1,2) AND official_kind = 1) OR
          (item_kind = 3 AND official_kind = 2)) THEN
    RAISE EXCEPTION 'Customer item and official identifier kinds do not match';
  END IF;
  IF EXISTS (
    SELECT 1 FROM catalog_official_mappings x
    WHERE x.item_id = NEW.item_id
      AND x.effective_from_utc < COALESCE(NEW.effective_to_utc, 'infinity'::timestamptz)
      AND NEW.effective_from_utc < COALESCE(x.effective_to_utc, 'infinity'::timestamptz)
  ) THEN
    RAISE EXCEPTION 'Overlapping tax identifier mapping windows for item';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_phase06_tax_mapping_guard
 BEFORE INSERT OR UPDATE OR DELETE ON catalog_official_mappings
 FOR EACH ROW EXECUTE FUNCTION phase06_tax_mapping_guard();
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Never disable protections while any historical mapping exists.
        migrationBuilder.Sql("""
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM catalog_official_mappings) THEN
   RAISE EXCEPTION 'Cannot remove tax mapping guard with historical mappings present';
 END IF;
END $$;
DROP TRIGGER trg_phase06_tax_mapping_guard ON catalog_official_mappings;
DROP FUNCTION phase06_tax_mapping_guard();
""");
    }
}
