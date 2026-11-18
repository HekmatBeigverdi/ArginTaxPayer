using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261118150000_AddSubscriptionAdjustments")]
public sealed class AddSubscriptionAdjustments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE subscription_credit_entries DROP CONSTRAINT IF EXISTS subscription_credit_entries_operation_type_check;
        ALTER TABLE subscription_credit_entries DROP CONSTRAINT IF EXISTS ck_credit_reservation_key;
        ALTER TABLE subscription_credit_entries ADD CONSTRAINT subscription_credit_entries_operation_type_check
          CHECK(operation_type IN ('grant','reserve','consume','release','admin_adjustment'));
        ALTER TABLE subscription_credit_entries ADD CONSTRAINT ck_credit_reservation_key
          CHECK ((operation_type IN ('grant','admin_adjustment') AND reservation_key IS NULL)
             OR (operation_type NOT IN ('grant','admin_adjustment') AND reservation_key IS NOT NULL));
        CREATE TABLE subscription_adjustments (
          id uuid PRIMARY KEY,
          customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          subscription_id uuid NOT NULL REFERENCES customer_subscriptions(id) ON DELETE RESTRICT,
          actor_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          operation_key varchar(160) NOT NULL UNIQUE,
          kind integer NOT NULL CHECK(kind IN (1,2)),
          previous_validity_days integer NULL,
          new_validity_days integer NULL,
          previous_invoice_unit_limit bigint NULL,
          new_invoice_unit_limit bigint NULL,
          negative_gross_irr bigint NOT NULL CHECK(negative_gross_irr>=0),
          negative_partner_share_irr bigint NOT NULL CHECK(negative_partner_share_irr>=0),
          negative_root_share_irr bigint NOT NULL CHECK(negative_root_share_irr>=0),
          reason varchar(1000) NOT NULL CHECK(length(trim(reason))>0),
          created_at_utc timestamptz NOT NULL,
          CHECK(negative_gross_irr=negative_partner_share_irr+negative_root_share_irr)
        );
        CREATE INDEX ix_subscription_adjustments_customer ON subscription_adjustments(customer_user_id,created_at_utc);
        CREATE OR REPLACE FUNCTION subscription_adjustments_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Subscription adjustments are append-only'; END $fn$;
        CREATE TRIGGER trg_subscription_adjustments_immutable BEFORE UPDATE OR DELETE ON subscription_adjustments
          FOR EACH ROW EXECUTE FUNCTION subscription_adjustments_immutable();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE subscription_credit_entries DROP CONSTRAINT IF EXISTS subscription_credit_entries_operation_type_check;
        ALTER TABLE subscription_credit_entries DROP CONSTRAINT IF EXISTS ck_credit_reservation_key;
        ALTER TABLE subscription_credit_entries ADD CONSTRAINT subscription_credit_entries_operation_type_check
          CHECK(operation_type IN ('grant','reserve','consume','release'));
        ALTER TABLE subscription_credit_entries ADD CONSTRAINT ck_credit_reservation_key
          CHECK ((operation_type='grant' AND reservation_key IS NULL)
             OR (operation_type<>'grant' AND reservation_key IS NOT NULL));
        DROP TABLE subscription_adjustments;
        DROP FUNCTION IF EXISTS subscription_adjustments_immutable();
        """);
}
