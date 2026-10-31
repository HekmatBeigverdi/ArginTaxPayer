using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ArginTaxPayer.Infrastructure.Persistence;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101020000_AddPhase12CommercialOrders")]
public sealed class AddPhase12CommercialOrders : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE commercial_orders (
          id uuid PRIMARY KEY,
          customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          plan_id uuid NOT NULL REFERENCES subscription_plan_catalog(id) ON DELETE RESTRICT,
          price_version_id uuid NOT NULL REFERENCES subscription_price_versions(id) ON DELETE RESTRICT,
          root_admin_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          idempotency_key varchar(160) NOT NULL,
          gross_amount_irr bigint NOT NULL CHECK(gross_amount_irr>=0),
          collected_amount_irr bigint NOT NULL DEFAULT 0 CHECK(collected_amount_irr>=0),
          status integer NOT NULL DEFAULT 1 CHECK(status IN(1,2,3,4)),
          issued_at_utc timestamptz NOT NULL,
          revision bigint NOT NULL DEFAULT 1 CHECK(revision>0),
          CONSTRAINT ck_commercial_order_receivable CHECK(collected_amount_irr<=gross_amount_irr),
          CONSTRAINT ux_commercial_order_request UNIQUE(customer_user_id,idempotency_key)
        );
        CREATE TABLE commercial_invoice_snapshots (
          order_id uuid PRIMARY KEY REFERENCES commercial_orders(id) ON DELETE RESTRICT,
          invoice_number varchar(48) NOT NULL UNIQUE,
          invoice_json jsonb NOT NULL,
          created_at_utc timestamptz NOT NULL
        );
        CREATE INDEX ix_commercial_order_customer ON commercial_orders(customer_user_id,issued_at_utc DESC);
        CREATE INDEX ix_commercial_order_partner ON commercial_orders(partner_user_id,issued_at_utc DESC);
        CREATE OR REPLACE FUNCTION phase12_commercial_invoice_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Commercial invoice snapshots are immutable'; END $fn$;
        CREATE TRIGGER trg_commercial_invoice_immutable
          BEFORE UPDATE OR DELETE ON commercial_invoice_snapshots FOR EACH ROW
          EXECUTE FUNCTION phase12_commercial_invoice_immutable();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE commercial_invoice_snapshots;
        DROP TABLE commercial_orders;
        DROP FUNCTION IF EXISTS phase12_commercial_invoice_immutable();
        """);
}
