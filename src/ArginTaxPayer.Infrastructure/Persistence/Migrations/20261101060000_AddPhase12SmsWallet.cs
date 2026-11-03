using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261101060000_AddPhase12SmsWallet")]
public sealed class AddPhase12SmsWallet : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE sms_wallet_accounts (
          customer_user_id uuid PRIMARY KEY REFERENCES identity_users(id) ON DELETE RESTRICT,
          granted_units bigint NOT NULL DEFAULT 0 CHECK(granted_units>=0),
          spent_units bigint NOT NULL DEFAULT 0 CHECK(spent_units>=0),
          revision bigint NOT NULL DEFAULT 1 CHECK(revision>0),
          CONSTRAINT ck_sms_wallet_balance CHECK (spent_units<=granted_units)
        );
        CREATE TABLE sms_wallet_events (
          id uuid PRIMARY KEY,
          customer_user_id uuid NOT NULL REFERENCES sms_wallet_accounts(customer_user_id) ON DELETE RESTRICT,
          operation_key varchar(160) NOT NULL,
          kind integer NOT NULL CHECK(kind IN (1,2)),
          units bigint NOT NULL CHECK(units>0),
          approved_by_level1_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          reference varchar(160) NOT NULL,
          recorded_at_utc timestamptz NOT NULL,
          CONSTRAINT ux_sms_wallet_operation UNIQUE(customer_user_id,operation_key),
          CONSTRAINT ck_sms_wallet_proof CHECK (
            (kind=1 AND approved_by_level1_user_id IS NOT NULL)
            OR (kind=2 AND approved_by_level1_user_id IS NULL))
        );
        CREATE INDEX ix_sms_wallet_events_customer ON sms_wallet_events(customer_user_id,recorded_at_utc DESC);
        CREATE OR REPLACE FUNCTION phase12_sms_event_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'SMS credit events are append-only'; END $fn$;
        CREATE TRIGGER trg_sms_wallet_event_immutable BEFORE UPDATE OR DELETE ON sms_wallet_events
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_event_immutable();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE sms_wallet_events;
        DROP TABLE sms_wallet_accounts;
        DROP FUNCTION IF EXISTS phase12_sms_event_immutable();
        """);
}
