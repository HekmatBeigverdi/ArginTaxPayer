using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261016120000_AddPhase06CatalogFoundations")]
public sealed class AddPhase06CatalogFoundations : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // Additive only. No implicit data import: verified RC_UMGS.ST belongs to Step04.
        m.Sql("""

CREATE TABLE catalog_reference_releases (
 id uuid PRIMARY KEY, source_document varchar(80) NOT NULL, edition varchar(80) NOT NULL,
 source_sha256 varchar(64) NOT NULL, source_reference varchar(1000) NOT NULL,
 state integer NOT NULL, imported_at_utc timestamptz NOT NULL, activated_at_utc timestamptz NULL,
 CONSTRAINT ck_catalog_release_state CHECK(state BETWEEN 1 AND 4),
 CONSTRAINT ck_catalog_release_hash CHECK(length(source_sha256)=64));
CREATE UNIQUE INDEX ux_catalog_release_source_hash ON catalog_reference_releases(source_document, source_sha256);

CREATE TABLE official_measurement_units (
 id uuid PRIMARY KEY, release_id uuid NOT NULL REFERENCES catalog_reference_releases(id) ON DELETE RESTRICT,
 code varchar(8) NOT NULL, title varchar(256) NOT NULL, status integer NOT NULL,
 CONSTRAINT ck_official_unit_code CHECK(code ~ '^[0-9]{1,8}$'));
CREATE UNIQUE INDEX ux_official_units_release_code ON official_measurement_units(release_id,code);

CREATE TABLE official_goods_service_entries (
 id uuid PRIMARY KEY, release_id uuid NOT NULL REFERENCES catalog_reference_releases(id) ON DELETE RESTRICT,
 code varchar(64) NOT NULL, title varchar(256) NOT NULL, kind integer NOT NULL, verification_status integer NOT NULL,
 CONSTRAINT ck_official_goods_kind CHECK(kind IN (1,2)),
 CONSTRAINT ck_official_goods_verification CHECK(verification_status IN (1,2)));
CREATE UNIQUE INDEX ux_official_goods_release_kind_code ON official_goods_service_entries(release_id,kind,code);

CREATE TABLE vat_classification_entries (
 id uuid PRIMARY KEY, release_id uuid NOT NULL REFERENCES catalog_reference_releases(id) ON DELETE RESTRICT,
 code varchar(64) NOT NULL, legal_basis varchar(512) NOT NULL, rate_percent numeric(8,4) NULL,
 effective_from_utc timestamptz NOT NULL, effective_to_utc timestamptz NULL,
 CONSTRAINT ck_vat_interval CHECK(effective_to_utc IS NULL OR effective_to_utc > effective_from_utc),
 CONSTRAINT ck_vat_percentage CHECK(rate_percent IS NULL OR (rate_percent >= 0 AND rate_percent <= 100)));
CREATE UNIQUE INDEX ux_vat_release_code_from ON vat_classification_entries(release_id,code,effective_from_utc);

CREATE TABLE customer_catalog_items (
 id uuid PRIMARY KEY, customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
 internal_code varchar(100) NOT NULL, title varchar(256) NOT NULL, kind integer NOT NULL,
 base_unit_id uuid NOT NULL REFERENCES official_measurement_units(id) ON DELETE RESTRICT,
 status integer NOT NULL, revision bigint NOT NULL DEFAULT 1,
 created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz NOT NULL,
 CONSTRAINT ck_catalog_item_kind CHECK(kind IN (1,2,3)),
 CONSTRAINT ck_catalog_item_status CHECK(status BETWEEN 1 AND 4),
 CONSTRAINT ck_catalog_item_revision CHECK(revision > 0));
CREATE UNIQUE INDEX ux_customer_catalog_internal_code ON customer_catalog_items(customer_user_id,internal_code);
CREATE INDEX ix_customer_catalog_base_unit ON customer_catalog_items(base_unit_id);

CREATE TABLE catalog_item_unit_conversions (
 id uuid PRIMARY KEY, item_id uuid NOT NULL REFERENCES customer_catalog_items(id) ON DELETE RESTRICT,
 from_unit_id uuid NOT NULL REFERENCES official_measurement_units(id) ON DELETE RESTRICT,
 to_base_numerator bigint NOT NULL, to_base_denominator bigint NOT NULL,
 rounding_policy integer NOT NULL, effective_from_utc timestamptz NOT NULL, effective_to_utc timestamptz NULL,
 CONSTRAINT ck_catalog_conversion_ratio CHECK(to_base_numerator > 0 AND to_base_denominator > 0),
 CONSTRAINT ck_catalog_conversion_round CHECK(rounding_policy IN (1,2,3)),
 CONSTRAINT ck_catalog_conversion_interval CHECK(effective_to_utc IS NULL OR effective_to_utc > effective_from_utc));
CREATE UNIQUE INDEX ux_catalog_conversion_item_unit_from ON catalog_item_unit_conversions(item_id,from_unit_id,effective_from_utc);
CREATE INDEX ix_catalog_conversion_unit ON catalog_item_unit_conversions(from_unit_id);

CREATE TABLE catalog_official_mappings (
 id uuid PRIMARY KEY, item_id uuid NOT NULL REFERENCES customer_catalog_items(id) ON DELETE RESTRICT,
 official_entry_id uuid NOT NULL REFERENCES official_goods_service_entries(id) ON DELETE RESTRICT,
 effective_from_utc timestamptz NOT NULL, effective_to_utc timestamptz NULL, revision bigint NOT NULL DEFAULT 1,
 CONSTRAINT ck_catalog_mapping_revision CHECK(revision > 0),
 CONSTRAINT ck_catalog_mapping_interval CHECK(effective_to_utc IS NULL OR effective_to_utc > effective_from_utc));
CREATE UNIQUE INDEX ux_catalog_mapping_item_from ON catalog_official_mappings(item_id,effective_from_utc);
CREATE INDEX ix_catalog_mapping_official ON catalog_official_mappings(official_entry_id);

""");
    }

    protected override void Down(MigrationBuilder m)
    {
        // Destructive rollback, only for a clean disposable test DB with no catalog data.
        m.Sql("""
DROP TABLE catalog_official_mappings;
DROP TABLE catalog_item_unit_conversions;
DROP TABLE customer_catalog_items;
DROP TABLE vat_classification_entries;
DROP TABLE official_goods_service_entries;
DROP TABLE official_measurement_units;
DROP TABLE catalog_reference_releases;
""");
    }
}
