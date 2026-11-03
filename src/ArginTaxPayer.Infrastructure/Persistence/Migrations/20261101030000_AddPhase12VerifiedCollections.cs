using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101030000_AddPhase12VerifiedCollections")]
public sealed class AddPhase12VerifiedCollections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE commercial_collections (
          id uuid PRIMARY KEY,
          order_id uuid NOT NULL REFERENCES commercial_orders(id) ON DELETE RESTRICT,
          customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          channel integer NOT NULL CHECK(channel IN (1,2,3)),
          amount_irr bigint NOT NULL CHECK(amount_irr>0),
          external_reference varchar(160) NOT NULL CHECK(length(trim(external_reference))>0),
          verified_at_utc timestamptz NOT NULL,
          verified_by_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          verifier_reference varchar(160) NOT NULL,
          CONSTRAINT ux_commercial_collection_reference UNIQUE(channel,external_reference),
          CONSTRAINT ck_commercial_collection_proof CHECK(
             (channel IN (1,2) AND verified_by_user_id IS NOT NULL)
             OR (channel=3 AND verified_by_user_id IS NULL AND length(trim(verifier_reference))>0))
        );
        CREATE INDEX ix_commercial_collection_order ON commercial_collections(order_id,verified_at_utc);
        CREATE OR REPLACE FUNCTION phase12_collection_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Verified collections are append-only'; END $fn$;
        CREATE TRIGGER trg_collection_immutable BEFORE UPDATE OR DELETE ON commercial_collections
          FOR EACH ROW EXECUTE FUNCTION phase12_collection_immutable();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE commercial_collections;
        DROP FUNCTION IF EXISTS phase12_collection_immutable();
        """);
}
