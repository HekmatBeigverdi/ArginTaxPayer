using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261102010000_AddQuantityRenewalAndOrderRejection")]
public sealed class AddQuantityRenewalAndOrderRejection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial_order_activations
          DROP CONSTRAINT IF EXISTS commercial_order_activations_subscription_id_key;
        CREATE TABLE commercial_order_activation_expiries (
          order_id uuid PRIMARY KEY REFERENCES commercial_order_activations(order_id) ON DELETE RESTRICT,
          expires_at_utc timestamptz NOT NULL
        );
        INSERT INTO commercial_order_activation_expiries(order_id,expires_at_utc)
        SELECT a.order_id,s.expires_at_utc
        FROM commercial_order_activations a
        JOIN customer_subscriptions s ON s.id=a.subscription_id;
        CREATE OR REPLACE FUNCTION phase12_activation_expiry_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Commercial activation expiry snapshots are immutable'; END $fn$;
        CREATE TRIGGER trg_commercial_activation_expiry_immutable
          BEFORE UPDATE OR DELETE ON commercial_order_activation_expiries FOR EACH ROW
          EXECUTE FUNCTION phase12_activation_expiry_immutable();

        CREATE TABLE commercial_order_rejections (
          order_id uuid PRIMARY KEY REFERENCES commercial_orders(id) ON DELETE RESTRICT,
          rejected_by_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          rejected_at_utc timestamptz NOT NULL
        );
        CREATE OR REPLACE FUNCTION phase12_order_rejection_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Commercial order rejection records are immutable'; END $fn$;
        CREATE TRIGGER trg_commercial_order_rejection_immutable
          BEFORE UPDATE OR DELETE ON commercial_order_rejections FOR EACH ROW
          EXECUTE FUNCTION phase12_order_rejection_immutable();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS commercial_order_rejections;
        DROP FUNCTION IF EXISTS phase12_order_rejection_immutable();
        DROP TABLE IF EXISTS commercial_order_activation_expiries;
        DROP FUNCTION IF EXISTS phase12_activation_expiry_immutable();
        DO $rollback$
        BEGIN
          IF EXISTS (
            SELECT subscription_id FROM commercial_order_activations
            GROUP BY subscription_id HAVING COUNT(*) > 1
          ) THEN
            RAISE EXCEPTION 'Cannot restore unique subscription activations while quantity orders share a subscription';
          END IF;
          ALTER TABLE commercial_order_activations
            ADD CONSTRAINT commercial_order_activations_subscription_id_key UNIQUE(subscription_id);
        END
        $rollback$;
        """);
}
