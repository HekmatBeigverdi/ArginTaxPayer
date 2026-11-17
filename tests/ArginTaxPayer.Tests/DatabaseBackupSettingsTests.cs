using System.Text.Json;
using ArginTaxPayer.Domain.Administration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class DatabaseBackupSettingsTests
{
    [Fact]
    public void Defaults_use_daily_schedule_and_twelve_monthly_remote_points()
    {
        var settings = DatabaseBackupSettings.Default;

        Assert.Equal(DatabaseBackupScheduleKind.Daily, settings.LocalScheduleKind);
        Assert.Equal(24, settings.IntervalHours);
        Assert.Equal(12, settings.MonthlyRetentionCount);
        Assert.Equal(21, settings.FtpPort);
        Assert.False(settings.FtpPasswordConfigured);
        settings.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Interval_schedule_rejects_hours_outside_one_to_twenty_four(int hours)
    {
        var settings = DatabaseBackupSettings.Default with
        {
            LocalScheduleKind = DatabaseBackupScheduleKind.IntervalHours,
            IntervalHours = hours
        };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Remote_retention_rejects_values_outside_supported_window(int months)
    {
        var settings = DatabaseBackupSettings.Default with { MonthlyRetentionCount = months };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Fact]
    public void Settings_json_never_contains_password_or_encryption_key_fields()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(DatabaseBackupSettings.Default));
        var properties = json.RootElement.EnumerateObject().Select(x => x.Name).ToArray();

        Assert.DoesNotContain("FtpPassword", properties);
        Assert.DoesNotContain("EncryptionKey", properties);
        Assert.Contains("FtpPasswordConfigured", properties);
        Assert.Contains("EncryptionKeyVersion", properties);
    }

    [Fact]
    public void Backup_control_authorization_requires_active_level1_with_mfa()
    {
        Assert.False(BackupAdministrationAuthorization.Allows(false, ["Level1"], true));
        Assert.False(BackupAdministrationAuthorization.Allows(true, ["Level2"], true));
        Assert.False(BackupAdministrationAuthorization.Allows(true, ["Level1"], false));
        Assert.True(BackupAdministrationAuthorization.Allows(true, ["Level1"], true));
    }

    [Theory]
    [InlineData("/backups/../escape")]
    [InlineData("/backups\nNOOP")]
    public void Remote_directory_rejects_path_traversal_and_control_characters(string path)
    {
        Assert.Throws<ArgumentException>(() => (DatabaseBackupSettings.Default with { RemoteDirectory = path }).Validate());
    }
}
