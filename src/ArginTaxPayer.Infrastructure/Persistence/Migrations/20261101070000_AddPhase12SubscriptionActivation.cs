using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101070000_AddPhase12SubscriptionActivation")]
public sealed class AddPhase12SubscriptionActivation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE commercial_order_activations (
            order_id uuid PRIMARY KEY REFERENCES commercial_orders(id) ON DELETE RESTRICT,
            subscription_id uuid NOT NULL UNIQUE REFERENCES customer_subscriptions(id) ON DELETE RESTRICT,
            activated_by_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
            verified_at_utc timestamptz NOT NULL
        );
        CREATE OR REPLACE FUNCTION phase12_activation_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Activated order associations are immutable'; END $fn$;
        CREATE TRIGGER trg_activation_immutable BEFORE UPDATE OR DELETE ON commercial_order_activations
          FOR EACH ROW EXECUTE FUNCTION phase12_activation_immutable();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE commercial_order_activations;
        DROP FUNCTION IF EXISTS phase12_activation_immutable();
        """);
}
