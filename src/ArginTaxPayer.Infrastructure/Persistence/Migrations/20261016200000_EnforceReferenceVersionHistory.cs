using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261016200000_EnforceReferenceVersionHistory")]
public sealed class EnforceReferenceVersionHistory : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // Database-backed defense in depth; no existing row is edited or seeded.
        m.Sql("""
CREATE UNIQUE INDEX ux_catalog_single_active_source
ON catalog_reference_releases (source_document)
WHERE state = 3;

CREATE FUNCTION phase06_reference_release_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Reference releases cannot be deleted';
    END IF;
    IF (OLD.id, OLD.source_document, OLD.edition, OLD.source_sha256,
        OLD.source_reference, OLD.imported_at_utc)
       IS DISTINCT FROM
       (NEW.id, NEW.source_document, NEW.edition, NEW.source_sha256,
        NEW.source_reference, NEW.imported_at_utc) THEN
        RAISE EXCEPTION 'Reference release identity/provenance is immutable';
    END IF;
    IF NEW.state <> OLD.state AND NOT (
        (OLD.state = 1 AND NEW.state = 2) OR
        (OLD.state = 2 AND NEW.state = 3) OR
        (OLD.state = 3 AND NEW.state = 4)) THEN
        RAISE EXCEPTION 'Invalid catalog reference release transition';
    END IF;
    IF NEW.state = 3 AND NEW.activated_at_utc IS NULL THEN
        RAISE EXCEPTION 'An active catalog release requires activated_at_utc';
    END IF;
    IF OLD.activated_at_utc IS NOT NULL AND
       NEW.activated_at_utc IS DISTINCT FROM OLD.activated_at_utc THEN
        RAISE EXCEPTION 'Activation timestamp is immutable';
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER trg_phase06_release_guard
BEFORE UPDATE OR DELETE ON catalog_reference_releases
FOR EACH ROW EXECUTE FUNCTION phase06_reference_release_guard();

CREATE FUNCTION phase06_reference_row_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    state_value integer;
    target_release uuid;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Official reference rows cannot be deleted';
    END IF;
    target_release := NEW.release_id;
    IF TG_OP = 'UPDATE' AND OLD.release_id <> NEW.release_id THEN
        RAISE EXCEPTION 'Moving a reference row across releases is forbidden';
    END IF;
    SELECT state INTO state_value
    FROM catalog_reference_releases
    WHERE id = target_release
    FOR UPDATE;
    IF state_value IS DISTINCT FROM 1 THEN
        RAISE EXCEPTION 'Official reference changes require a staged release';
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER trg_phase06_units_guard
BEFORE INSERT OR UPDATE OR DELETE ON official_measurement_units
FOR EACH ROW EXECUTE FUNCTION phase06_reference_row_guard();

CREATE TRIGGER trg_phase06_goods_guard
BEFORE INSERT OR UPDATE OR DELETE ON official_goods_service_entries
FOR EACH ROW EXECUTE FUNCTION phase06_reference_row_guard();

CREATE TRIGGER trg_phase06_vat_guard
BEFORE INSERT OR UPDATE OR DELETE ON vat_classification_entries
FOR EACH ROW EXECUTE FUNCTION phase06_reference_row_guard();

CREATE FUNCTION phase06_vat_interval_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    -- Previous trigger locks the parent release, serializing sibling writes.
    IF EXISTS (
        SELECT 1 FROM vat_classification_entries v
        WHERE v.release_id = NEW.release_id
          AND v.code = NEW.code
          AND v.context_key IS NOT DISTINCT FROM NEW.context_key
          AND v.id <> NEW.id
          AND v.effective_from_utc < COALESCE(NEW.effective_to_utc, 'infinity'::timestamptz)
          AND NEW.effective_from_utc < COALESCE(v.effective_to_utc, 'infinity'::timestamptz)
    ) THEN
        RAISE EXCEPTION 'Overlapping VAT classification intervals for release, code and context';
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER trg_phase06_vat_interval_guard
BEFORE INSERT OR UPDATE ON vat_classification_entries
FOR EACH ROW EXECUTE FUNCTION phase06_vat_interval_guard();
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        // Safety: undoing persistence protections while catalog data exists needs review.
        m.Sql("""
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM catalog_reference_releases) THEN
   RAISE EXCEPTION 'Cannot remove historical reference protection while releases exist';
 END IF;
END $$;
DROP TRIGGER trg_phase06_vat_interval_guard ON vat_classification_entries;
DROP FUNCTION phase06_vat_interval_guard();
DROP TRIGGER trg_phase06_vat_guard ON vat_classification_entries;
DROP TRIGGER trg_phase06_goods_guard ON official_goods_service_entries;
DROP TRIGGER trg_phase06_units_guard ON official_measurement_units;
DROP FUNCTION phase06_reference_row_guard();
DROP TRIGGER trg_phase06_release_guard ON catalog_reference_releases;
DROP FUNCTION phase06_reference_release_guard();
DROP INDEX ux_catalog_single_active_source;
""");
    }
}
