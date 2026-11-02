using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261102050000_AddSmsCustomerBillingEnforcement")]
public sealed class AddSmsCustomerBillingEnforcement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE application_settings ADD CONSTRAINT ck_application_sms_customer_billing_enabled CHECK (
          setting_key <> 'sms.service' OR value_json->'customer_billing_enabled' IS NULL OR
          jsonb_typeof(value_json->'customer_billing_enabled')='boolean'
        );
        ALTER TABLE sms_service_setting_events
          ADD COLUMN customer_billing_enabled boolean NOT NULL DEFAULT TRUE;
        ALTER TABLE sms_provider_dispatches
          ADD COLUMN customer_billing_enabled_snapshot boolean NOT NULL DEFAULT TRUE;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE sms_provider_dispatches DROP COLUMN customer_billing_enabled_snapshot;
        ALTER TABLE sms_service_setting_events DROP COLUMN customer_billing_enabled;
        ALTER TABLE application_settings DROP CONSTRAINT ck_application_sms_customer_billing_enabled;
        """);
}
