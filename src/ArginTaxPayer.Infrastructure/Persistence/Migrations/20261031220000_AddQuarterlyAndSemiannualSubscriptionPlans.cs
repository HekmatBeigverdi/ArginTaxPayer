using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261031220000_AddQuarterlyAndSemiannualSubscriptionPlans")]
public sealed class AddQuarterlyAndSemiannualSubscriptionPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT IF EXISTS ck_subscription_plan_kind;
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_plan_kind CHECK (kind IN (1,2,3,4,5));
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT IF EXISTS ck_subscription_plan_units;
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_plan_units CHECK
          ((kind IN (1,3,4,5) AND invoice_unit_limit IS NULL) OR (kind=2 AND invoice_unit_limit > 0));
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_term_days CHECK
          ((kind=1 AND validity_days BETWEEN 365 AND 366) OR
           (kind=2 AND validity_days BETWEEN 1 AND 366) OR
           (kind=3 AND validity_days BETWEEN 28 AND 31) OR
           (kind=4 AND validity_days BETWEEN 89 AND 92) OR
           (kind=5 AND validity_days BETWEEN 181 AND 184));
        ALTER TABLE customer_subscriptions DROP CONSTRAINT IF EXISTS ck_customer_subscription_kind;
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_kind CHECK (kind IN (1,2,3,4,5));
        ALTER TABLE customer_subscriptions DROP CONSTRAINT IF EXISTS ck_customer_subscription_units;
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_units CHECK
          ((kind IN (1,3,4,5) AND invoice_unit_limit IS NULL) OR (kind=2 AND invoice_unit_limit > 0));
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_term_days CHECK
          ((kind=1 AND validity_days BETWEEN 365 AND 366) OR
           (kind=2 AND validity_days BETWEEN 1 AND 366) OR
           (kind=3 AND validity_days BETWEEN 28 AND 31) OR
           (kind=4 AND validity_days BETWEEN 89 AND 92) OR
           (kind=5 AND validity_days BETWEEN 181 AND 184));
        CREATE OR REPLACE FUNCTION phase12_binding_integrity() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
          IF TG_OP <> 'INSERT' THEN
            RAISE EXCEPTION 'Single-taxpayer bindings are immutable';
          END IF;
          IF NOT EXISTS (
            SELECT 1 FROM customer_subscriptions s JOIN taxpayer_profiles t
              ON t.id=NEW.taxpayer_id AND t.customer_user_id=s.customer_user_id
            JOIN fiscal_memories m ON m.id=NEW.fiscal_memory_id AND m.taxpayer_id=t.id
            WHERE s.id=NEW.subscription_id AND s.customer_user_id=NEW.customer_user_id
              AND s.kind IN (1,3,4,5) AND s.state=2
          ) THEN
            RAISE EXCEPTION 'Single-taxpayer binding scope invalid';
          END IF;
          RETURN NEW;
        END $fn$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $fn$ BEGIN
          IF EXISTS(SELECT 1 FROM subscription_plan_catalog WHERE kind IN (4,5)) OR
             EXISTS(SELECT 1 FROM customer_subscriptions WHERE kind IN (4,5)) THEN
            RAISE EXCEPTION 'Cannot downgrade with quarterly or semiannual plans';
          END IF;
        END $fn$;
        ALTER TABLE customer_subscriptions DROP CONSTRAINT ck_customer_subscription_term_days;
        ALTER TABLE customer_subscriptions DROP CONSTRAINT IF EXISTS ck_customer_subscription_kind;
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_kind CHECK (kind IN (1,2,3));
        ALTER TABLE customer_subscriptions DROP CONSTRAINT IF EXISTS ck_customer_subscription_units;
        ALTER TABLE customer_subscriptions ADD CONSTRAINT ck_customer_subscription_units CHECK
          ((kind IN (1,3) AND invoice_unit_limit IS NULL) OR (kind=2 AND invoice_unit_limit > 0));
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT ck_subscription_term_days;
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT IF EXISTS ck_subscription_plan_kind;
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_plan_kind CHECK (kind IN (1,2,3));
        ALTER TABLE subscription_plan_catalog DROP CONSTRAINT IF EXISTS ck_subscription_plan_units;
        ALTER TABLE subscription_plan_catalog ADD CONSTRAINT ck_subscription_plan_units CHECK
          ((kind IN (1,3) AND invoice_unit_limit IS NULL) OR (kind=2 AND invoice_unit_limit > 0));
        """);
}
