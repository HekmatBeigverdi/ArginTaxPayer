using ArginTaxPayer.Domain.Administration;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Administration;

public sealed record DatabaseBackupSettingsUpdate(
    bool Enabled,
    DatabaseBackupScheduleKind LocalScheduleKind,
    int IntervalHours,
    bool RemoteEnabled,
    int MonthlyRetentionCount,
    string RemoteDirectory,
    string FtpHost,
    int FtpPort,
    string FtpUsername);

public sealed class DatabaseBackupSettingsService(
    ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity,
    IConfiguration configuration)
{
    private bool LocalDevelopmentMode =>
        configuration.GetValue("BackupController:LocalDevelopmentMode", false);

    public async Task<DatabaseBackupSettings> GetAsync(
        string? bearer,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireRootAsync(bearer, cancellationToken);
        return await ReadForRootAsync(actor, cancellationToken)
            ?? ApplyLocalDevelopmentSettings(DatabaseBackupSettings.Default);
    }

    public async Task<DatabaseBackupSettings> SaveAsync(
        string? bearer,
        DatabaseBackupSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var actor = await RequireRootAsync(bearer, cancellationToken);
        var current = await ReadForRootAsync(actor, cancellationToken)
            ?? DatabaseBackupSettings.Default;
        var candidate = current with
        {
            Enabled = update.Enabled,
            LocalScheduleKind = update.LocalScheduleKind,
            IntervalHours = update.IntervalHours,
            RemoteEnabled = LocalDevelopmentMode ? false : update.RemoteEnabled,
            MonthlyRetentionCount = update.MonthlyRetentionCount,
            RemoteDirectory = update.RemoteDirectory.Trim(),
            FtpHost = update.FtpHost.Trim(),
            FtpPort = update.FtpPort,
            FtpUsername = update.FtpUsername.Trim(),
            FtpPasswordConfigured = false,
            Revision = current.Revision,
            UpdatedAtUtc = DateTime.UtcNow
        };
        candidate.Validate();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO application_settings(
              scope_user_id,setting_key,value_json,updated_by_user_id,updated_at_utc)
            VALUES({actor},{DatabaseBackupSettings.SettingKey},jsonb_build_object(
              'enabled',{candidate.Enabled},
              'local_schedule_kind',{candidate.LocalScheduleKind.ToString()},
              'interval_hours',{candidate.IntervalHours},
              'remote_enabled',{candidate.RemoteEnabled},
              'monthly_retention_count',{candidate.MonthlyRetentionCount},
              'local_path',{candidate.LocalPath},
              'remote_directory',{candidate.RemoteDirectory},
              'ftp_host',{candidate.FtpHost},
              'ftp_port',{candidate.FtpPort},
              'ftp_username',{candidate.FtpUsername},
              'encryption_key_version',{candidate.EncryptionKeyVersion}),
              {actor},{candidate.UpdatedAtUtc})
            ON CONFLICT(scope_user_id,setting_key) DO UPDATE SET
              value_json=EXCLUDED.value_json,
              revision=application_settings.revision+1,
              updated_by_user_id=EXCLUDED.updated_by_user_id,
              updated_at_utc=EXCLUDED.updated_at_utc
            """, cancellationToken);

        return await ReadForRootAsync(actor, cancellationToken)
            ?? throw new InvalidOperationException("Saved database backup settings could not be read.");
    }

    public async Task<DatabaseBackupSettings?> ReadForRootAsync(
        Guid rootUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Database.SqlQueryRaw<SettingsRow>("""
            SELECT
              (value_json->>'enabled')::boolean AS "Enabled",
              value_json->>'local_schedule_kind' AS "ScheduleKind",
              (value_json->>'interval_hours')::integer AS "IntervalHours",
              (value_json->>'remote_enabled')::boolean AS "RemoteEnabled",
              (value_json->>'monthly_retention_count')::integer AS "MonthlyRetentionCount",
              value_json->>'local_path' AS "LocalPath",
              value_json->>'remote_directory' AS "RemoteDirectory",
              value_json->>'ftp_host' AS "FtpHost",
              (value_json->>'ftp_port')::integer AS "FtpPort",
              value_json->>'ftp_username' AS "FtpUsername",
              value_json->>'encryption_key_version' AS "EncryptionKeyVersion",
              revision AS "Revision", updated_at_utc AS "UpdatedAtUtc"
            FROM application_settings
            WHERE scope_user_id={0} AND setting_key='database.backup'
            """, rootUserId).ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return LocalDevelopmentMode
                ? ApplyLocalDevelopmentSettings(DatabaseBackupSettings.Default)
                : null;

        var row = rows.Single();
        if (!Enum.TryParse<DatabaseBackupScheduleKind>(row.ScheduleKind, out var scheduleKind))
            throw new InvalidOperationException("Stored database backup schedule kind is invalid.");
        var settings = new DatabaseBackupSettings(
            row.Enabled, scheduleKind, row.IntervalHours, row.RemoteEnabled,
            row.MonthlyRetentionCount, row.LocalPath, row.RemoteDirectory,
            row.FtpHost, row.FtpPort, row.FtpUsername, false,
            row.EncryptionKeyVersion, row.Revision, row.UpdatedAtUtc);
        settings.Validate();
        return ApplyLocalDevelopmentSettings(settings);
    }

    public async Task<DatabaseBackupSettings?> ReadLatestAsync(CancellationToken cancellationToken = default)
    {
        var owners = await db.Database.SqlQueryRaw<Guid>("""
            SELECT scope_user_id AS "Value" FROM application_settings
            WHERE setting_key='database.backup'
            ORDER BY updated_at_utc DESC LIMIT 1
            """).ToListAsync(cancellationToken);
        return owners.Count == 0 ? null : await ReadForRootAsync(owners[0], cancellationToken);
    }

    private DatabaseBackupSettings ApplyLocalDevelopmentSettings(DatabaseBackupSettings settings) =>
        LocalDevelopmentMode
            ? settings with
            {
                LocalPath = configuration["BackupController:LocalPath"] ?? settings.LocalPath,
                EncryptionKeyVersion = configuration["BackupController:DevelopmentKeyVersion"] ?? "dev-mac-1",
                RemoteEnabled = false
            }
            : settings;

    public async Task<Guid> RequireRootAsync(
        string? bearer,
        CancellationToken cancellationToken = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, cancellationToken)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var profile = await db.IdentityUsers.AsNoTracking()
            .Where(user => user.Id == actor)
            .Select(user => new
            {
                user.IsActive,
                Roles = user.Roles.Select(role => role.Role).ToArray(),
                MfaEnabled = db.IdentityMfaStates.Any(state =>
                    state.UserId == actor && state.EnabledAtUtc != null)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null || !BackupAdministrationAuthorization.Allows(
                profile.IsActive, profile.Roles, profile.MfaEnabled))
            throw new UnauthorizedAccessException("Level 1 MFA required.");
        return actor;
    }

    private sealed class SettingsRow
    {
        public bool Enabled { get; set; }
        public string ScheduleKind { get; set; } = "";
        public int IntervalHours { get; set; }
        public bool RemoteEnabled { get; set; }
        public int MonthlyRetentionCount { get; set; }
        public string LocalPath { get; set; } = "";
        public string RemoteDirectory { get; set; } = "";
        public string FtpHost { get; set; } = "";
        public int FtpPort { get; set; }
        public string FtpUsername { get; set; } = "";
        public string EncryptionKeyVersion { get; set; } = "";
        public long Revision { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
