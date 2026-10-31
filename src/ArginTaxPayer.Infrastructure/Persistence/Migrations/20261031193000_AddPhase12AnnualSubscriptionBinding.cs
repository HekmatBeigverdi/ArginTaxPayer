using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261031193000_AddPhase12AnnualSubscriptionBinding")]
public sealed class AddPhase12AnnualSubscriptionBinding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE subscription_annual_bindings (
          subscription_id uuid PRIMARY KEY REFERENCES customer_subscriptions(id) ON DELETE RESTRICT,
          customer_user_id uuid NOT NULL,
          taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
          fiscal_memory_id uuid NOT NULL REFERENCES fiscal_memories(id) ON DELETE RESTRICT,
          verified_connected_at_utc timestamptz NOT NULL,
          evidence_reference varchar(128) NOT NULL,
          CONSTRAINT ck_subscription_binding_evidence CHECK(length(trim(evidence_reference)) > 0)
        );
        CREATE TABLE subscription_binding_exceptions (
          id uuid PRIMARY KEY,
          subscription_id uuid NOT NULL REFERENCES customer_subscriptions(id) ON DELETE RESTRICT,
          customer_user_id uuid NOT NULL,
          requester_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          proposed_taxpayer_id uuid NOT NULL REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT,
          proposed_memory_id uuid NOT NULL REFERENCES fiscal_memories(id) ON DELETE RESTRICT,
          reason varchar(1000) NOT NULL,
          status integer NOT NULL DEFAULT 1,
          reviewer_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          requested_at_utc timestamptz NOT NULL,
          reviewed_at_utc timestamptz NULL,
          CONSTRAINT ck_subscription_exception_status CHECK(status IN (1,2,3)),
          CONSTRAINT ck_subscription_exception_reason CHECK(length(trim(reason)) > 0),
          CONSTRAINT ck_subscription_exception_review CHECK(
            (status=1 AND reviewer_user_id IS NULL AND reviewed_at_utc IS NULL)
            OR (status IN (2,3) AND reviewer_user_id IS NOT NULL AND reviewed_at_utc IS NOT NULL))
        );
        CREATE INDEX ix_subscription_binding_exception_history
          ON subscription_binding_exceptions(subscription_id,requested_at_utc);
        CREATE UNIQUE INDEX ux_subscription_binding_exception_pending
          ON subscription_binding_exceptions(subscription_id) WHERE status=1;
        CREATE OR REPLACE FUNCTION phase12_binding_integrity() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
          IF TG_OP <> 'INSERT' THEN
            RAISE EXCEPTION 'Annual bindings are immutable; historical rebinding is prohibited';
          END IF;
          IF NOT EXISTS (
            SELECT 1 FROM customer_subscriptions s JOIN taxpayer_profiles t
            ON t.id=NEW.taxpayer_id AND t.customer_user_id=s.customer_user_id
            JOIN fiscal_memories m ON m.id=NEW.fiscal_memory_id AND m.taxpayer_id=t.id
            WHERE s.id=NEW.subscription_id AND s.customer_user_id=NEW.customer_user_id
              AND s.kind=1 AND s.state=2
          ) THEN
            RAISE EXCEPTION 'Annual binding scope, plan or active subscription invalid';
          END IF;
          RETURN NEW;
        END
        $fn$;
        CREATE TRIGGER trg_phase12_binding_integrity
          BEFORE INSERT OR UPDATE OR DELETE ON subscription_annual_bindings
          FOR EACH ROW EXECUTE FUNCTION phase12_binding_integrity();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS subscription_binding_exceptions;
        DROP TABLE IF EXISTS subscription_annual_bindings;
        DROP FUNCTION IF EXISTS phase12_binding_integrity();
        """);
}
