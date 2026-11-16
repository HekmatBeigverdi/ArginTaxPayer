using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261115130000_AddDatabaseBackupControl")]
public sealed class AddDatabaseBackupControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE application_settings
        ADD CONSTRAINT ck_application_settings_database_backup CHECK (
          setting_key <> 'database.backup' OR (
            jsonb_typeof(value_json->'enabled')='boolean' AND
            value_json->>'local_schedule_kind' IN ('Daily','IntervalHours') AND
            value_json->>'interval_hours' ~ '^(?:[1-9]|1[0-9]|2[0-4])$' AND
            jsonb_typeof(value_json->'remote_enabled')='boolean' AND
            value_json->>'monthly_retention_count' ~ '^(?:[1-9]|[1-9][0-9]|1[01][0-9]|120)$' AND
            value_json->>'local_path' LIKE '/%' AND
            position(chr(10) in value_json->>'local_path')=0 AND
            position(chr(13) in value_json->>'local_path')=0 AND
            value_json->>'remote_directory' LIKE '/%' AND
            position(chr(10) in value_json->>'remote_directory')=0 AND
            position(chr(13) in value_json->>'remote_directory')=0 AND
            position(chr(92) in value_json->>'remote_directory')=0 AND
            value_json->>'remote_directory' !~ '(^|/)\.\.(/|$)' AND
            length(value_json->>'ftp_host') BETWEEN 1 AND 253 AND
            position(chr(10) in value_json->>'ftp_host')=0 AND
            position(chr(13) in value_json->>'ftp_host')=0 AND
            value_json->>'ftp_port'='21' AND
            length(value_json->>'ftp_username') BETWEEN 1 AND 256 AND
            position(chr(10) in value_json->>'ftp_username')=0 AND
            position(chr(13) in value_json->>'ftp_username')=0 AND
            length(value_json->>'encryption_key_version') BETWEEN 1 AND 32 AND
            NOT (value_json ? 'ftp_password') AND
            NOT (value_json ? 'encryption_key')
          )
        );
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE application_settings
        DROP CONSTRAINT IF EXISTS ck_application_settings_database_backup;
        """);
}
