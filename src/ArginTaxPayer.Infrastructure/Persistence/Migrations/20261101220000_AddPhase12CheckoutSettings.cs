using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;
[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101220000_AddPhase12CheckoutSettings")]
public sealed class AddPhase12CheckoutSettings : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
  CREATE TABLE application_settings (
    scope_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
    setting_key varchar(120) NOT NULL CHECK(length(trim(setting_key))>0),
    value_json jsonb NOT NULL CHECK(jsonb_typeof(value_json)='object'),
    updated_by_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
    updated_at_utc timestamptz NOT NULL,
    revision bigint NOT NULL DEFAULT 1 CHECK(revision>0),
    PRIMARY KEY(scope_user_id,setting_key),
    CONSTRAINT ck_application_vat_setting CHECK (
      setting_key<>'checkout.vat' OR (
        jsonb_typeof(value_json->'basis_points')='number' AND
        (value_json->>'basis_points') ~ '^[0-9]{1,5}
  CREATE TABLE commercial_checkout_order_options (
    order_id uuid PRIMARY KEY REFERENCES commercial_orders(id) ON DELETE RESTRICT,
    official_invoice_requested boolean NOT NULL,
    vat_basis_points integer NOT NULL CHECK(vat_basis_points BETWEEN 0 AND 10000),
    base_amount_irr bigint NOT NULL CHECK(base_amount_irr>=0),
    vat_amount_irr bigint NOT NULL CHECK(vat_amount_irr>=0),
    total_amount_irr bigint NOT NULL CHECK(total_amount_irr=base_amount_irr+vat_amount_irr),
    payment_method integer NOT NULL CHECK(payment_method IN (1,2)),
    purchase_purpose integer NOT NULL DEFAULT 1 CHECK(purchase_purpose IN (1,2,3,4)),
    base_subscription_id uuid NULL REFERENCES customer_subscriptions(id) ON DELETE RESTRICT,
    CHECK ((purchase_purpose=4 AND base_subscription_id IS NOT NULL) OR
           (purchase_purpose<>4 AND base_subscription_id IS NULL))
  );
  CREATE OR REPLACE FUNCTION phase12_checkout_options_immutable() RETURNS trigger LANGUAGE plpgsql AS $f$
  BEGIN RAISE EXCEPTION 'Purchase options are immutable'; END $f$;
  CREATE TRIGGER trg_checkout_options_immutable BEFORE UPDATE OR DELETE ON commercial_checkout_order_options
    FOR EACH ROW EXECUTE FUNCTION phase12_checkout_options_immutable();
  """);
 protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
  DROP TABLE commercial_checkout_order_options;
  DROP TABLE application_settings;
  DROP FUNCTION IF EXISTS phase12_checkout_options_immutable();
  """);
}
 AND
        (value_json->>'basis_points')::integer BETWEEN 0 AND 10000
      )
    )
  );
  CREATE INDEX ix_application_settings_key ON application_settings(setting_key);
  CREATE TABLE commercial_checkout_order_options (
    order_id uuid PRIMARY KEY REFERENCES commercial_orders(id) ON DELETE RESTRICT,
    official_invoice_requested boolean NOT NULL,
    vat_basis_points integer NOT NULL CHECK(vat_basis_points BETWEEN 0 AND 10000),
    base_amount_irr bigint NOT NULL CHECK(base_amount_irr>=0),
    vat_amount_irr bigint NOT NULL CHECK(vat_amount_irr>=0),
    total_amount_irr bigint NOT NULL CHECK(total_amount_irr=base_amount_irr+vat_amount_irr),
    payment_method integer NOT NULL CHECK(payment_method IN (1,2)),
    purchase_purpose integer NOT NULL DEFAULT 1 CHECK(purchase_purpose IN (1,2,3,4)),
    base_subscription_id uuid NULL REFERENCES customer_subscriptions(id) ON DELETE RESTRICT,
    CHECK ((purchase_purpose=4 AND base_subscription_id IS NOT NULL) OR
           (purchase_purpose<>4 AND base_subscription_id IS NULL))
  );
  CREATE OR REPLACE FUNCTION phase12_checkout_options_immutable() RETURNS trigger LANGUAGE plpgsql AS $f$
  BEGIN RAISE EXCEPTION 'Purchase options are immutable'; END $f$;
  CREATE TRIGGER trg_checkout_options_immutable BEFORE UPDATE OR DELETE ON commercial_checkout_order_options
    FOR EACH ROW EXECUTE FUNCTION phase12_checkout_options_immutable();
  """);
 protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
  DROP TABLE commercial_checkout_order_options;
  DROP TABLE commercial_checkout_tax_settings;
  DROP FUNCTION IF EXISTS phase12_checkout_options_immutable();
  """);
}
