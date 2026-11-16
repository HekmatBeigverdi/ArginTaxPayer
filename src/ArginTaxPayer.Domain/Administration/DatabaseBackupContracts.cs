namespace ArginTaxPayer.Domain.Administration;

public enum DatabaseBackupScheduleKind
{
    Daily = 1,
    IntervalHours = 2
}

/// <summary>Public backup policy and metadata; never contains secret bytes.</summary>
public sealed record DatabaseBackupSettings(
    bool Enabled,
    DatabaseBackupScheduleKind LocalScheduleKind,
    int IntervalHours,
    bool RemoteEnabled,
    int MonthlyRetentionCount,
    string LocalPath,
    string RemoteDirectory,
    string FtpHost,
    int FtpPort,
    string FtpUsername,
    bool FtpPasswordConfigured,
    string EncryptionKeyVersion,
    long Revision,
    DateTime UpdatedAtUtc)
{
    public const string SettingKey = "database.backup";

    public static DatabaseBackupSettings Default { get; } = new(
        Enabled: false,
        LocalScheduleKind: DatabaseBackupScheduleKind.Daily,
        IntervalHours: 24,
        RemoteEnabled: false,
        MonthlyRetentionCount: 12,
        LocalPath: "/var/lib/argintaxpayer/database-backups",
        RemoteDirectory: "/",
        FtpHost: "ftp.argin.ir",
        FtpPort: 21,
        FtpUsername: "backup@argin.ir",
        FtpPasswordConfigured: false,
        EncryptionKeyVersion: "1",
        Revision: 0,
        UpdatedAtUtc: DateTime.UnixEpoch);

    public void Validate()
    {
        if (!Enum.IsDefined(LocalScheduleKind))
            throw new ArgumentOutOfRangeException(nameof(LocalScheduleKind));
        if (IntervalHours is < 1 or > 24)
            throw new ArgumentOutOfRangeException(nameof(IntervalHours));
        if (MonthlyRetentionCount is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(MonthlyRetentionCount));
        if (string.IsNullOrWhiteSpace(LocalPath) || LocalPath.Contains('\r') || LocalPath.Contains('\n') || !Path.IsPathFullyQualified(LocalPath))
            throw new ArgumentException("Local backup path must be absolute.", nameof(LocalPath));
        if (string.IsNullOrWhiteSpace(RemoteDirectory) || !RemoteDirectory.StartsWith('/') ||
            RemoteDirectory.Contains('\r') || RemoteDirectory.Contains('\n') || RemoteDirectory.Contains('\\') ||
            RemoteDirectory.Split('/').Any(part => part is "." or ".."))
            throw new ArgumentException("Remote backup directory must be absolute.", nameof(RemoteDirectory));
        if (string.IsNullOrWhiteSpace(FtpHost) || FtpHost.Length > 253 ||
            FtpHost.Contains('\r') || FtpHost.Contains('\n') || Uri.CheckHostName(FtpHost) == UriHostNameType.Unknown)
            throw new ArgumentException("A valid FTPS host is required.", nameof(FtpHost));
        if (FtpPort != 21)
            throw new ArgumentOutOfRangeException(nameof(FtpPort), "Explicit FTPS uses port 21.");
        if (string.IsNullOrWhiteSpace(FtpUsername) || FtpUsername.Length > 256 ||
            FtpUsername.Contains('\r') || FtpUsername.Contains('\n'))
            throw new ArgumentException("A valid FTPS username is required.", nameof(FtpUsername));
        if (Revision < 0)
            throw new ArgumentOutOfRangeException(nameof(Revision));
    }
}

public static class BackupAdministrationAuthorization
{
    public static bool Allows(bool active, IEnumerable<string> roles, bool mfaEnabled) =>
        active && mfaEnabled && roles.Contains("Level1", StringComparer.Ordinal);
}
