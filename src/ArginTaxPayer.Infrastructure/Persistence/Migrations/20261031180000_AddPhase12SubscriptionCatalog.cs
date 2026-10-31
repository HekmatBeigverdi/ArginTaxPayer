using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261031180000_AddPhase12SubscriptionCatalog")]
public sealed class AddPhase12SubscriptionCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE subscription_plan_catalog (
          id uuid PRIMARY KEY,
          code varchar(80) NOT NULL UNIQUE,
          kind integer NOT NULL,
          validity_days integer NOT NULL,
          invoice_unit_limit bigint NULL,
          is_active boolean NOT NULL DEFAULT false,
          created_at_utc timestamptz NOT NULL,
          CONSTRAINT ck_subscription_plan_kind CHECK (kind IN (1,2)),
          CONSTRAINT ck_subscription_plan_validity CHECK (validity_days BETWEEN 1 AND 366),
          CONSTRAINT ck_subscription_plan_units CHECK (
            (kind=1 AND invoice_unit_limit IS NULL) OR
            (kind=2 AND invoice_unit_limit > 0))
        );
        CREATE TABLE subscription_price_versions (
          id uuid PRIMARY KEY,
          plan_id uuid NOT NULL REFERENCES subscription_plan_catalog(id) ON DELETE RESTRICT,
          root_admin_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          audience integer NOT NULL,
          price_irr bigint NOT NULL,
          effective_from_utc timestamptz NOT NULL,
          effective_until_utc timestamptz NULL,
          created_at_utc timestamptz NOT NULL,
          CONSTRAINT ck_subscription_price_audience CHECK (
            (audience=1 AND partner_user_id IS NULL) OR
            (audience=2 AND partner_user_id IS NOT NULL)),
          CONSTRAINT ck_subscription_price_nonnegative CHECK (price_irr >= 0),
          CONSTRAINT ck_subscription_price_window CHECK (
            effective_until_utc IS NULL OR effective_until_utc > effective_from_utc)
        );
        CREATE INDEX ix_subscription_price_owner ON subscription_price_versions
          (root_admin_user_id,partner_user_id,plan_id,effective_from_utc);
        CREATE TABLE customer_subscriptions (
          id uuid PRIMARY KEY,
          customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          plan_id uuid NOT NULL REFERENCES subscription_plan_catalog(id) ON DELETE RESTRICT,
          price_version_id uuid NOT NULL REFERENCES subscription_price_versions(id) ON DELETE RESTRICT,
          kind integer NOT NULL,
          validity_days integer NOT NULL,
          invoice_unit_limit bigint NULL,
          price_irr bigint NOT NULL,
          root_admin_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          state integer NOT NULL DEFAULT 1,
          purchased_at_utc timestamptz NOT NULL,
          activated_at_utc timestamptz NULL,
          expires_at_utc timestamptz NULL,
          revision bigint NOT NULL DEFAULT 1,
          CONSTRAINT ck_customer_subscription_kind CHECK (kind IN (1,2)),
          CONSTRAINT ck_customer_subscription_units CHECK (
            (kind=1 AND invoice_unit_limit IS NULL) OR
            (kind=2 AND invoice_unit_limit > 0)),
          CONSTRAINT ck_customer_subscription_days CHECK (validity_days BETWEEN 1 AND 366),
          CONSTRAINT ck_customer_subscription_price CHECK (price_irr >= 0),
          CONSTRAINT ck_customer_subscription_state CHECK (state IN (1,2,3,4)),
          CONSTRAINT ck_customer_subscription_revision CHECK (revision > 0),
          CONSTRAINT ck_customer_subscription_activation CHECK (
            (state=1 AND activated_at_utc IS NULL AND expires_at_utc IS NULL) OR
            (state<>1 AND activated_at_utc IS NOT NULL AND expires_at_utc > activated_at_utc))
        );
        CREATE INDEX ix_customer_subscription_owner
          ON customer_subscriptions(customer_user_id,purchased_at_utc);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS customer_subscriptions;
        DROP TABLE IF EXISTS subscription_price_versions;
        DROP TABLE IF EXISTS subscription_plan_catalog;
        """);
}
