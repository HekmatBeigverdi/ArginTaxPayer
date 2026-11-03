using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261102030000_AddPhase12SmsMoneyLedger")]
public sealed class AddPhase12SmsMoneyLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE application_settings ADD CONSTRAINT ck_application_sms_service_setting CHECK (
          setting_key <> 'sms.service' OR COALESCE((
            jsonb_typeof(value_json->'service_fee_basis_points') = 'number' AND
            (value_json->>'service_fee_basis_points') ~ '^[0-9]{1,5}$' AND
            (value_json->>'service_fee_basis_points')::integer BETWEEN 0 AND 10000 AND
            jsonb_typeof(value_json->'reservation_ceiling_irr') = 'number' AND
            (value_json->>'reservation_ceiling_irr') ~ '^[1-9][0-9]{0,18}$' AND
            (value_json->>'reservation_ceiling_irr')::numeric <= 9223372036854775807
          ),false)
        );
        CREATE TABLE sms_service_setting_events (
          id uuid PRIMARY KEY,
          root_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          revision bigint NOT NULL CHECK(revision > 0),
          service_fee_basis_points integer NOT NULL CHECK(service_fee_basis_points BETWEEN 0 AND 10000),
          reservation_ceiling_irr bigint NOT NULL CHECK(reservation_ceiling_irr > 0),
          recorded_by_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          recorded_at_utc timestamptz NOT NULL,
          UNIQUE(root_level1_user_id,revision)
        );
        CREATE INDEX ix_sms_setting_events_root_time
          ON sms_service_setting_events(root_level1_user_id,recorded_at_utc DESC,revision DESC);

        CREATE TABLE sms_credit_accounts (
          customer_user_id uuid PRIMARY KEY REFERENCES identity_users(id) ON DELETE RESTRICT,
          granted_irr bigint NOT NULL DEFAULT 0 CHECK(granted_irr >= 0),
          spent_irr bigint NOT NULL DEFAULT 0 CHECK(spent_irr >= 0),
          reserved_irr bigint NOT NULL DEFAULT 0 CHECK(reserved_irr >= 0),
          revision bigint NOT NULL DEFAULT 1 CHECK(revision > 0),
          updated_at_utc timestamptz NOT NULL,
          CONSTRAINT ck_sms_credit_available CHECK(
            spent_irr <= granted_irr AND reserved_irr <= granted_irr - spent_irr)
        );
        CREATE TABLE sms_provider_dispatches (
          id uuid PRIMARY KEY,
          client_reference_id varchar(80) NOT NULL UNIQUE CHECK(length(trim(client_reference_id)) > 0),
          actor_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          sms_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          root_level1_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_level2_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          destination_hash varchar(64) NOT NULL CHECK(destination_hash ~ '^[0-9A-F]{64}$'),
          purpose varchar(24) NOT NULL CHECK(purpose IN ('register','login','reset','notification')),
          state smallint NOT NULL CHECK(state BETWEEN 1 AND 7),
          reservation_irr bigint NOT NULL DEFAULT 0 CHECK(reservation_irr >= 0),
          service_fee_basis_points integer NULL CHECK(service_fee_basis_points BETWEEN 0 AND 10000),
          reservation_ceiling_snapshot_irr bigint NULL CHECK(reservation_ceiling_snapshot_irr > 0),
          provider_message_id varchar(120) NULL,
          created_at_utc timestamptz NOT NULL,
          updated_at_utc timestamptz NOT NULL,
          CONSTRAINT ck_sms_dispatch_partner_scope CHECK(
            partner_level2_user_id IS NULL OR root_level1_user_id IS NOT NULL)
        );
        CREATE INDEX ix_sms_dispatch_customer_time
          ON sms_provider_dispatches(sms_user_id,created_at_utc DESC,id);
        CREATE INDEX ix_sms_dispatch_scope_time
          ON sms_provider_dispatches(root_level1_user_id,partner_level2_user_id,created_at_utc DESC,id);
        CREATE INDEX ix_sms_dispatch_destination_state
          ON sms_provider_dispatches(destination_hash,state,created_at_utc DESC);

        CREATE TABLE sms_provider_expense_events (
          id uuid PRIMARY KEY,
          dispatch_id uuid NOT NULL UNIQUE REFERENCES sms_provider_dispatches(id) ON DELETE RESTRICT,
          actor_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          sms_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          root_level1_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_level2_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          purpose varchar(24) NOT NULL CHECK(purpose IN ('register','login','reset','notification')),
          provider_message_id varchar(120) NULL,
          raw_provider_cost numeric(20,4) NOT NULL CHECK(raw_provider_cost >= 0),
          raw_provider_cost_unit varchar(16) NOT NULL CHECK(length(trim(raw_provider_cost_unit)) > 0),
          provider_cost_irr bigint NOT NULL CHECK(provider_cost_irr >= 0),
          provider_sent_at_utc timestamptz NULL,
          recorded_at_utc timestamptz NOT NULL,
          CONSTRAINT ck_sms_provider_partner_scope CHECK(
            partner_level2_user_id IS NULL OR root_level1_user_id IS NOT NULL)
        );
        CREATE INDEX ix_sms_provider_expense_customer_time
          ON sms_provider_expense_events(sms_user_id,recorded_at_utc DESC,id);
        CREATE INDEX ix_sms_provider_expense_scope_time
          ON sms_provider_expense_events(root_level1_user_id,partner_level2_user_id,recorded_at_utc DESC,id);
        CREATE INDEX ix_sms_provider_expense_actor_time
          ON sms_provider_expense_events(actor_user_id,recorded_at_utc DESC,id);

        CREATE TABLE sms_customer_sms_charges (
          id uuid PRIMARY KEY,
          dispatch_id uuid NOT NULL UNIQUE REFERENCES sms_provider_dispatches(id) ON DELETE RESTRICT,
          customer_user_id uuid NOT NULL REFERENCES sms_credit_accounts(customer_user_id) ON DELETE RESTRICT,
          root_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_level2_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          provider_cost_irr bigint NOT NULL CHECK(provider_cost_irr >= 0),
          service_fee_basis_points integer NOT NULL CHECK(service_fee_basis_points BETWEEN 0 AND 10000),
          service_fee_irr bigint NOT NULL CHECK(service_fee_irr >= 0),
          customer_charge_irr bigint NOT NULL CHECK(customer_charge_irr = provider_cost_irr + service_fee_irr),
          recorded_at_utc timestamptz NOT NULL,
          CONSTRAINT ck_sms_customer_charge_partner_scope CHECK(
            partner_level2_user_id IS NULL OR root_level1_user_id IS NOT NULL)
        );
        CREATE INDEX ix_sms_customer_charge_scope_time
          ON sms_customer_sms_charges(root_level1_user_id,partner_level2_user_id,recorded_at_utc DESC,id);
        CREATE INDEX ix_sms_customer_charge_user_time
          ON sms_customer_sms_charges(customer_user_id,recorded_at_utc DESC,id);

        CREATE TABLE sms_dispatch_customer_links (
          dispatch_id uuid PRIMARY KEY REFERENCES sms_provider_dispatches(id) ON DELETE RESTRICT,
          customer_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          root_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_level2_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          linked_at_utc timestamptz NOT NULL,
          CONSTRAINT ck_sms_dispatch_link_partner_scope CHECK(
            partner_level2_user_id IS NULL OR root_level1_user_id IS NOT NULL)
        );
        CREATE INDEX ix_sms_dispatch_customer_links_user
          ON sms_dispatch_customer_links(customer_user_id,linked_at_utc DESC);

        CREATE TABLE sms_provider_dispatch_events (
          id uuid PRIMARY KEY,
          dispatch_id uuid NOT NULL REFERENCES sms_provider_dispatches(id) ON DELETE RESTRICT,
          state smallint NOT NULL CHECK(state BETWEEN 1 AND 7),
          detail_code varchar(64) NOT NULL CHECK(length(trim(detail_code)) > 0),
          recorded_at_utc timestamptz NOT NULL
        );
        CREATE INDEX ix_sms_dispatch_events_dispatch_time
          ON sms_provider_dispatch_events(dispatch_id,recorded_at_utc,id);

        CREATE TABLE sms_credit_events (
          id uuid PRIMARY KEY,
          customer_user_id uuid NOT NULL REFERENCES sms_credit_accounts(customer_user_id) ON DELETE RESTRICT,
          operation_key varchar(180) NOT NULL CHECK(length(trim(operation_key)) > 0),
          event_kind smallint NOT NULL CHECK(event_kind BETWEEN 1 AND 6),
          granted_delta_irr bigint NOT NULL DEFAULT 0,
          spent_delta_irr bigint NOT NULL DEFAULT 0,
          reserved_delta_irr bigint NOT NULL DEFAULT 0,
          dispatch_id uuid NULL REFERENCES sms_provider_dispatches(id) ON DELETE RESTRICT,
          reference varchar(160) NOT NULL CHECK(length(trim(reference)) > 0),
          recorded_at_utc timestamptz NOT NULL,
          UNIQUE(customer_user_id,operation_key),
          CONSTRAINT ck_sms_credit_event_nonzero CHECK(
            granted_delta_irr <> 0 OR spent_delta_irr <> 0 OR reserved_delta_irr <> 0),
          CONSTRAINT ck_sms_credit_event_shape CHECK(
            (event_kind BETWEEN 1 AND 3 AND granted_delta_irr > 0 AND spent_delta_irr = 0 AND reserved_delta_irr = 0) OR
            (event_kind = 4 AND granted_delta_irr = 0 AND spent_delta_irr = 0 AND reserved_delta_irr > 0) OR
            (event_kind = 5 AND granted_delta_irr = 0 AND spent_delta_irr >= 0 AND reserved_delta_irr <= 0 AND
              (spent_delta_irr > 0 OR reserved_delta_irr < 0 OR dispatch_id IS NOT NULL)) OR
            (event_kind = 6 AND granted_delta_irr = 0 AND spent_delta_irr = 0 AND reserved_delta_irr < 0))
        );
        CREATE INDEX ix_sms_credit_events_customer_time
          ON sms_credit_events(customer_user_id,recorded_at_utc DESC,id);
        CREATE UNIQUE INDEX ux_sms_credit_dispatch_event
          ON sms_credit_events(dispatch_id,event_kind) WHERE dispatch_id IS NOT NULL;

        CREATE OR REPLACE FUNCTION phase12_sms_ledger_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'SMS accounting evidence is append-only'; END $fn$;
        CREATE TRIGGER trg_sms_setting_events_immutable BEFORE UPDATE OR DELETE ON sms_service_setting_events
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_ledger_immutable();
        CREATE TRIGGER trg_sms_provider_expense_immutable BEFORE UPDATE OR DELETE ON sms_provider_expense_events
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_ledger_immutable();
        CREATE TRIGGER trg_sms_customer_charge_immutable BEFORE UPDATE OR DELETE ON sms_customer_sms_charges
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_ledger_immutable();
        CREATE TRIGGER trg_sms_dispatch_links_immutable BEFORE UPDATE OR DELETE ON sms_dispatch_customer_links
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_ledger_immutable();
        CREATE TRIGGER trg_sms_dispatch_events_immutable BEFORE UPDATE OR DELETE ON sms_provider_dispatch_events
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_ledger_immutable();
        CREATE TRIGGER trg_sms_credit_events_immutable BEFORE UPDATE OR DELETE ON sms_credit_events
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_ledger_immutable();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE sms_credit_events;
        DROP TABLE sms_provider_dispatch_events;
        DROP TABLE sms_dispatch_customer_links;
        DROP TABLE sms_customer_sms_charges;
        DROP TABLE sms_provider_expense_events;
        DROP TABLE sms_provider_dispatches;
        DROP TABLE sms_credit_accounts;
        DROP TABLE sms_service_setting_events;
        ALTER TABLE application_settings DROP CONSTRAINT ck_application_sms_service_setting;
        DROP FUNCTION IF EXISTS phase12_sms_ledger_immutable();
        """);
}
