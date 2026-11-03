using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261031210000_AlignSingleTaxpayerCommercialPlans")]
public sealed class AlignSingleTaxpayerCommercialPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT IF EXISTS ck_subscription_plan_kind;
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_plan_kind CHECK (kind IN (1,2,3));
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT IF EXISTS ck_subscription_plan_units;
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_plan_units CHECK
          ((kind IN (1,3) AND invoice_unit_limit IS NULL) OR (kind=2 AND invoice_unit_limit > 0));
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_month_days CHECK
          (kind<>3 OR validity_days BETWEEN 28 AND 31);
        ALTER TABLE customer_subscriptions DROP CONSTRAINT IF EXISTS ck_customer_subscription_kind;
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_kind CHECK (kind IN (1,2,3));
        ALTER TABLE customer_subscriptions DROP CONSTRAINT IF EXISTS ck_customer_subscription_units;
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_units CHECK
          ((kind IN (1,3) AND invoice_unit_limit IS NULL) OR (kind=2 AND invoice_unit_limit > 0));
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_month_days CHECK
          (kind<>3 OR validity_days BETWEEN 28 AND 31);
        CREATE OR REPLACE FUNCTION phase12_binding_integrity() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
          IF TG_OP <> 'INSERT' THEN
            RAISE EXCEPTION 'Single-taxpayer bindings are immutable; use existing Level1-controlled memory process';
          END IF;
          IF NOT EXISTS (
            SELECT 1 FROM customer_subscriptions s JOIN taxpayer_profiles t
              ON t.id=NEW.taxpayer_id AND t.customer_user_id=s.customer_user_id
            JOIN fiscal_memories m ON m.id=NEW.fiscal_memory_id AND m.taxpayer_id=t.id
            WHERE s.id=NEW.subscription_id AND s.customer_user_id=NEW.customer_user_id
              AND s.kind IN (1,3) AND s.state=2
          ) THEN
            RAISE EXCEPTION 'Single-taxpayer binding scope or subscription invalid';
          END IF;
          RETURN NEW;
        END
        $fn$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $fn$ BEGIN
          IF EXISTS(SELECT 1 FROM subscription_plan_catalog WHERE kind=3)
             OR EXISTS(SELECT 1 FROM customer_subscriptions WHERE kind=3) THEN
            RAISE EXCEPTION 'Cannot downgrade: monthly subscriptions exist';
          END IF;
        END $fn$;
        ALTER TABLE customer_subscriptions DROP CONSTRAINT ck_customer_subscription_month_days;
        ALTER TABLE customer_subscriptions DROP CONSTRAINT IF EXISTS ck_customer_subscription_kind;
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_kind CHECK(kind IN (1,2));
        ALTER TABLE customer_subscriptions DROP CONSTRAINT IF EXISTS ck_customer_subscription_units;
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_units CHECK
          ((kind=1 AND invoice_unit_limit IS NULL) OR (kind=2 AND invoice_unit_limit>0));
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT ck_subscription_month_days;
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT IF EXISTS ck_subscription_plan_kind;
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_plan_kind CHECK(kind IN (1,2));
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT IF EXISTS ck_subscription_plan_units;
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_plan_units CHECK
          ((kind=1 AND invoice_unit_limit IS NULL) OR (kind=2 AND invoice_unit_limit>0));
        """);
}
