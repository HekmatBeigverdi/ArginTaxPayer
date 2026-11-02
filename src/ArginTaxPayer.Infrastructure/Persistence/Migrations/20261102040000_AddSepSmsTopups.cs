using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261102040000_AddSepSmsTopups")]
public sealed class AddSepSmsTopups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE sms_sep_terminal_settings (
          root_level1_user_id uuid PRIMARY KEY REFERENCES identity_users(id) ON DELETE RESTRICT,
          terminal_number integer NOT NULL CHECK(terminal_number > 0),
          assigned_admin_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          revision bigint NOT NULL CHECK(revision > 0),
          updated_by_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          updated_at_utc timestamptz NOT NULL
        );
        CREATE TABLE sms_sep_terminal_setting_events (
          id uuid PRIMARY KEY,
          root_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          terminal_number integer NOT NULL CHECK(terminal_number > 0),
          assigned_admin_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          revision bigint NOT NULL CHECK(revision > 0),
          changed_by_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          recorded_at_utc timestamptz NOT NULL,
          UNIQUE(root_level1_user_id,revision)
        );
        CREATE INDEX ix_sms_sep_terminal_events_root_time
          ON sms_sep_terminal_setting_events(root_level1_user_id,recorded_at_utc DESC);

        CREATE TABLE sms_credit_topups (
          id uuid PRIMARY KEY,
          customer_user_id uuid NOT NULL REFERENCES sms_credit_accounts(customer_user_id) ON DELETE RESTRICT,
          root_level1_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          partner_level2_user_id uuid NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          initiated_by_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          assigned_admin_user_id uuid NOT NULL REFERENCES identity_users(id) ON DELETE RESTRICT,
          terminal_number integer NOT NULL CHECK(terminal_number > 0),
          amount_irr bigint NOT NULL CHECK(amount_irr BETWEEN 10000 AND 100000000000),
          res_num varchar(64) NOT NULL UNIQUE CHECK(length(trim(res_num)) BETWEEN 16 AND 64),
          provider_ref_num varchar(128) NULL,
          provider_trace_no varchar(80) NULL,
          callback_state varchar(40) NULL,
          state smallint NOT NULL CHECK(state BETWEEN 1 AND 6),
          created_at_utc timestamptz NOT NULL,
          updated_at_utc timestamptz NOT NULL,
          verified_at_utc timestamptz NULL,
          credited_at_utc timestamptz NULL,
          CONSTRAINT ck_sms_topup_partner_scope CHECK(partner_level2_user_id IS NULL OR root_level1_user_id IS NOT NULL)
        );
        CREATE UNIQUE INDEX ux_sms_topup_provider_ref ON sms_credit_topups(provider_ref_num) WHERE provider_ref_num IS NOT NULL;
        CREATE INDEX ix_sms_topup_customer_time ON sms_credit_topups(customer_user_id,created_at_utc DESC,id);
        CREATE INDEX ix_sms_topup_scope_time ON sms_credit_topups(root_level1_user_id,partner_level2_user_id,created_at_utc DESC,id);
        CREATE INDEX ix_sms_topup_state_time ON sms_credit_topups(state,updated_at_utc);

        CREATE TABLE sms_credit_topup_events (
          id uuid PRIMARY KEY,
          topup_id uuid NOT NULL REFERENCES sms_credit_topups(id) ON DELETE RESTRICT,
          event_kind varchar(40) NOT NULL CHECK(length(trim(event_kind))>0),
          callback_state varchar(40) NULL,
          provider_ref_num varchar(128) NULL,
          provider_trace_no varchar(80) NULL,
          verify_result_code integer NULL,
          verify_description varchar(240) NULL,
          reverse_result_code integer NULL,
          verified_amount_irr bigint NULL CHECK(verified_amount_irr>=0),
          verified_terminal_number integer NULL,
          recorded_at_utc timestamptz NOT NULL
        );
        CREATE INDEX ix_sms_topup_events_topup_time ON sms_credit_topup_events(topup_id,recorded_at_utc,id);
        CREATE TRIGGER trg_sms_topup_terminal_events_immutable BEFORE UPDATE OR DELETE ON sms_sep_terminal_setting_events
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_ledger_immutable();
        CREATE TRIGGER trg_sms_topup_events_immutable BEFORE UPDATE OR DELETE ON sms_credit_topup_events
          FOR EACH ROW EXECUTE FUNCTION phase12_sms_ledger_immutable();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE sms_credit_topup_events;
        DROP TABLE sms_credit_topups;
        DROP TABLE sms_sep_terminal_setting_events;
        DROP TABLE sms_sep_terminal_settings;
        """);
}
