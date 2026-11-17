using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class DatabaseBackupSchedulerTests
{
    [Fact]
    public void Daily_schedule_uses_next_utc_boundary()
    {
        var next = DatabaseBackupScheduler.GetNextRunUtc(
            new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc),
            ArginTaxPayer.Domain.Administration.DatabaseBackupScheduleKind.Daily, 24);
        Assert.Equal(new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Hourly_schedule_uses_interval_boundaries_and_validates_bounds()
    {
        var next = DatabaseBackupScheduler.GetNextRunUtc(new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc),
            ArginTaxPayer.Domain.Administration.DatabaseBackupScheduleKind.IntervalHours, 4);
        Assert.Equal(new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc), next);
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseBackupScheduler.GetNextRunUtc(
            DateTime.UtcNow, ArginTaxPayer.Domain.Administration.DatabaseBackupScheduleKind.IntervalHours, 25));
    }

    [Fact]
    public void Schema_acceptance_requires_exact_business_migrations_but_tolerates_backup_metadata_only_migration()
    {
        var expected = new[] { "20240101000000_Initial", "20261115130000_AddDatabaseBackupControl" };
        Assert.True(DatabaseSchemaCompatibility.Matches(expected, new[] { "20240101000000_Initial" }));
        Assert.False(DatabaseSchemaCompatibility.Matches(expected, Array.Empty<string>()));
        Assert.False(DatabaseSchemaCompatibility.Matches(expected, new[] { "20240101000000_Initial", "20261201000000_InvoiceChange" }));
    }

    [Fact]
    public async Task Interrupted_restore_activation_recovers_as_success_when_recovery_marker_is_active()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BackupOperationStore(path);
            var active = await store.StartAsync("restore-activate", Guid.NewGuid(), "recovery-only", CancellationToken.None);
            var recovered = await store.RecoverInterruptedAsync(CancellationToken.None, recoveryMode: true);
            Assert.Equal(active.Id, recovered!.Id);
            Assert.Equal("succeeded", recovered.Status);
            Assert.Contains("recovery-only mode", recovered.Message);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    [Fact]
    public async Task Concurrent_worker_start_requests_are_serialized_by_persistent_lock()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var stores = new[] { new BackupOperationStore(path), new BackupOperationStore(path) };
            var outcomes = await Task.WhenAll(stores.Select(async store =>
            {
                try { await store.StartAsync("manual", Guid.NewGuid(), "offline", CancellationToken.None); return "started"; }
                catch (InvalidOperationException) { return "rejected"; }
            }));
            Assert.Single(outcomes, outcome => outcome == "started");
            Assert.Single(outcomes, outcome => outcome == "rejected");
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    [Fact]
    public void Most_recent_due_time_tracks_schedule_without_local_timezone()
    {
        var now = new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc),
            DatabaseBackupHostedScheduler.MostRecentDueUtc(now,
                ArginTaxPayer.Domain.Administration.DatabaseBackupScheduleKind.Daily, 24));
        Assert.Equal(new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc),
            DatabaseBackupHostedScheduler.MostRecentDueUtc(now,
                ArginTaxPayer.Domain.Administration.DatabaseBackupScheduleKind.IntervalHours, 4));
    }

    [Fact]
    public async Task Operation_store_serializes_and_recovers_durable_status()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BackupOperationStore(path);
            var started = await store.StartAsync("scheduled", Guid.NewGuid(), "online", CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.StartAsync("manual", Guid.NewGuid(), "offline", CancellationToken.None));
            var recovered = await new BackupOperationStore(path).GetStatusAsync(CancellationToken.None);
            Assert.Equal(started.Id, recovered!.Id);
            Assert.Equal("running", recovered.Status);
            var interrupted = await new BackupOperationStore(path).RecoverInterruptedAsync(CancellationToken.None);
            Assert.Equal("failed", interrupted!.Status);
            Assert.Contains("restarted", interrupted.Message);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
