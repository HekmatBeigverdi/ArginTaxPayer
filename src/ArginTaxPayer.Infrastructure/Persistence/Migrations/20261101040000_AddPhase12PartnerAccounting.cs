using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101040000_AddPhase12PartnerAccounting")]
public sealed class AddPhase12PartnerAccounting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE partner_share_terms (
          id uuid PRIMARY KEY,
          root_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_basis_points integer NOT NULL CHECK (partner_basis_points BETWEEN 0 AND 10000),
          effective_from_utc timestamptz NOT NULL,
          effective_until_utc timestamptz NULL,
          approved_by_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          CHECK (effective_until_utc IS NULL OR effective_until_utc > effective_from_utc)
        );
        CREATE INDEX ix_partner_share_terms_scope ON partner_share_terms(root_user_id,partner_user_id,effective_from_utc);
        CREATE TABLE commercial_approved_costs (
          id uuid PRIMARY KEY,
          collection_id uuid NOT NULL REFERENCES commercial_collections(id) ON DELETE RESTRICT,
          amount_irr bigint NOT NULL CHECK (amount_irr>0),
          explanation varchar(1000) NOT NULL CHECK (length(trim(explanation))>0),
          approved_by_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          approved_at_utc timestamptz NOT NULL,
          CONSTRAINT ux_commercial_approved_cost UNIQUE(collection_id)
        );
        CREATE TABLE commercial_revenue_allocations (
          id uuid PRIMARY KEY,
          collection_id uuid NOT NULL UNIQUE REFERENCES commercial_collections(id) ON DELETE RESTRICT,
          root_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          share_term_id uuid NULL REFERENCES partner_share_terms(id) ON DELETE RESTRICT,
          gross_collected_irr bigint NOT NULL CHECK(gross_collected_irr>0),
          approved_cost_irr bigint NOT NULL CHECK(approved_cost_irr>=0),
          partner_share_irr bigint NOT NULL CHECK(partner_share_irr>=0),
          root_share_irr bigint NOT NULL CHECK(root_share_irr>=0),
          posted_at_utc timestamptz NOT NULL,
          CONSTRAINT ck_revenue_allocation_balance CHECK (
            gross_collected_irr = approved_cost_irr + partner_share_irr + root_share_irr),
          CONSTRAINT ck_revenue_direct_root CHECK (
            (partner_user_id IS NULL AND share_term_id IS NULL AND partner_share_irr=0)
            OR (partner_user_id IS NOT NULL AND share_term_id IS NOT NULL))
        );
        CREATE INDEX ix_revenue_allocations_root ON commercial_revenue_allocations(root_user_id,posted_at_utc);
        CREATE INDEX ix_revenue_allocations_partner ON commercial_revenue_allocations(partner_user_id,posted_at_utc);
        CREATE OR REPLACE FUNCTION phase12_finance_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Financial allocation evidence is append-only'; END $fn$;
        CREATE TRIGGER trg_partner_share_terms_immutable BEFORE UPDATE OR DELETE ON partner_share_terms
          FOR EACH ROW EXECUTE FUNCTION phase12_finance_immutable();
        CREATE TRIGGER trg_approved_costs_immutable BEFORE UPDATE OR DELETE ON commercial_approved_costs
          FOR EACH ROW EXECUTE FUNCTION phase12_finance_immutable();
        CREATE TRIGGER trg_revenue_allocations_immutable BEFORE UPDATE OR DELETE ON commercial_revenue_allocations
          FOR EACH ROW EXECUTE FUNCTION phase12_finance_immutable();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE commercial_revenue_allocations;
        DROP TABLE commercial_approved_costs;
        DROP TABLE partner_share_terms;
        DROP FUNCTION IF EXISTS phase12_finance_immutable();
        """);
}
