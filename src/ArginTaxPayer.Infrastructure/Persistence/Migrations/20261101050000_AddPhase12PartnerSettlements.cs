using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101050000_AddPhase12PartnerSettlements")]
public sealed class AddPhase12PartnerSettlements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE partner_settlement_events (
          id uuid PRIMARY KEY,
          allocation_id uuid NOT NULL REFERENCES commercial_revenue_allocations(id) ON DELETE RESTRICT,
          root_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          kind integer NOT NULL CHECK(kind IN (1,2)),
          amount_irr bigint NOT NULL CHECK(amount_irr>0),
          external_reference varchar(160) NOT NULL CHECK(length(trim(external_reference))>0),
          original_event_id uuid NULL REFERENCES partner_settlement_events(id) ON DELETE RESTRICT,
          recorded_by_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          reason varchar(1000) NOT NULL CHECK(length(trim(reason))>0),
          recorded_at_utc timestamptz NOT NULL,
          CONSTRAINT ux_settlement_partner_reference UNIQUE(root_user_id,external_reference),
          CONSTRAINT ck_settlement_reversal_reference CHECK
             ((kind=1 AND original_event_id IS NULL) OR (kind=2 AND original_event_id IS NOT NULL)),
          CONSTRAINT ux_settlement_single_reversal UNIQUE(original_event_id)
        );
        CREATE INDEX ix_partner_settlement_allocation ON partner_settlement_events(allocation_id,recorded_at_utc);
        CREATE INDEX ix_partner_settlement_partner ON partner_settlement_events(partner_user_id,recorded_at_utc);
        CREATE OR REPLACE FUNCTION phase12_settlement_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Settlement events are append-only'; END $fn$;
        CREATE TRIGGER trg_settlement_immutable BEFORE UPDATE OR DELETE ON partner_settlement_events
          FOR EACH ROW EXECUTE FUNCTION phase12_settlement_immutable();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE partner_settlement_events;
        DROP FUNCTION IF EXISTS phase12_settlement_immutable();
        """);
}
