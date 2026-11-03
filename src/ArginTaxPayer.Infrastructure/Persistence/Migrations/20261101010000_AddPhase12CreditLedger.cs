using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ArginTaxPayer.Infrastructure.Persistence;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101010000_AddPhase12CreditLedger")]
public sealed class AddPhase12CreditLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE subscription_credit_accounts (
          subscription_id uuid PRIMARY KEY REFERENCES customer_subscriptions(id) ON DELETE RESTRICT,
          customer_user_id uuid NOT NULL,
          granted_units bigint NOT NULL CHECK(granted_units >= 0),
          available_units bigint NOT NULL CHECK(available_units >= 0),
          reserved_units bigint NOT NULL CHECK(reserved_units >= 0),
          consumed_units bigint NOT NULL CHECK(consumed_units >= 0),
          revision bigint NOT NULL DEFAULT 1 CHECK(revision > 0),
          CONSTRAINT ck_credit_balance CHECK (available_units + reserved_units + consumed_units = granted_units)
        );
        CREATE TABLE subscription_credit_entries (
          id uuid PRIMARY KEY,
          subscription_id uuid NOT NULL REFERENCES subscription_credit_accounts(subscription_id) ON DELETE RESTRICT,
          customer_user_id uuid NOT NULL,
          operation_key varchar(160) NOT NULL,
          operation_type varchar(24) NOT NULL CHECK(operation_type IN ('grant','reserve','consume','release')),
          reservation_key varchar(160) NULL,
          units bigint NOT NULL CHECK(units>0),
          recorded_at_utc timestamptz NOT NULL,
          CONSTRAINT ux_credit_operation UNIQUE(subscription_id,operation_key),
          CONSTRAINT ck_credit_reservation_key CHECK (
            (operation_type='grant' AND reservation_key IS NULL)
            OR (operation_type<>'grant' AND reservation_key IS NOT NULL))
        );
        CREATE INDEX ix_credit_entries_reservation ON subscription_credit_entries(subscription_id,reservation_key,recorded_at_utc);
        CREATE OR REPLACE FUNCTION phase12_credit_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Credit entries are append-only'; END $fn$;
        CREATE TRIGGER trg_credit_immutable BEFORE UPDATE OR DELETE ON subscription_credit_entries
          FOR EACH ROW EXECUTE FUNCTION phase12_credit_immutable();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE subscription_credit_entries;
        DROP TABLE subscription_credit_accounts;
        DROP FUNCTION IF EXISTS phase12_credit_immutable();
        """);
}
