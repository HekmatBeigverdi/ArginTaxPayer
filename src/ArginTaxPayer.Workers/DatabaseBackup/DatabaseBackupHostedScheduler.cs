using System.Globalization;
using System.Text.Json;
using ArginTaxPayer.Domain.Administration;
using ArginTaxPayer.Infrastructure.Persistence.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed class DatabaseBackupHostedScheduler(
    IServiceScopeFactory scopes,
    RecoveryModeStore recovery,
    IConfiguration configuration,
    ILogger<DatabaseBackupHostedScheduler> logger) : BackgroundService
{
    private readonly string statePath = Path.Combine(
        configuration["BackupController:StateDirectory"] ?? "/var/lib/argintaxpayer/backup-controller",
        "last-scheduled-at-utc.txt");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckDueAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Database backup scheduler check failed ({ErrorType}).", ex.GetType().Name); }
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task CheckDueAsync(CancellationToken ct)
    {
        if ((await recovery.GetAsync(ct)).RecoveryMode) return;
        DatabaseBackupSettings? policy;
        using (var scope = scopes.CreateScope())
            policy = await scope.ServiceProvider.GetRequiredService<DatabaseBackupSettingsService>().ReadLatestAsync(ct);
        if (policy is not { Enabled: true }) return;
        var now = DateTime.UtcNow;
        var due = MostRecentDueUtc(now, policy.LocalScheduleKind, policy.IntervalHours);
        var previous = await ReadLastAttemptAsync(ct);
        if (previous >= due) return;

        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        await File.WriteAllTextAsync(statePath + ".partial", due.ToString("O", CultureInfo.InvariantCulture), ct);
        File.Move(statePath + ".partial", statePath, true);
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<BackupExecutionService>()
                .CreateAsync(Guid.Empty, "scheduled", policy, stopBusinessServices: false, ct);
            logger.LogInformation("Scheduled database backup completed at {BackupTimeUtc}.", due);
        }
        catch (Exception ex)
        {
            logger.LogError("Scheduled database backup failed ({ErrorType}).", ex.GetType().Name);
        }
    }

    private async Task<DateTime> ReadLastAttemptAsync(CancellationToken ct)
    {
        if (!File.Exists(statePath)) return DateTime.UnixEpoch;
        var text = await File.ReadAllTextAsync(statePath, ct);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : DateTime.UnixEpoch;
    }

    public static DateTime MostRecentDueUtc(DateTime nowUtc, DatabaseBackupScheduleKind kind, int intervalHours)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Scheduler clock must be UTC.", nameof(nowUtc));
        if (intervalHours is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(intervalHours));
        return kind switch
        {
            DatabaseBackupScheduleKind.Daily => nowUtc.Date,
            DatabaseBackupScheduleKind.IntervalHours => nowUtc.Date.AddHours(
                Math.Floor(nowUtc.TimeOfDay.TotalHours / intervalHours) * intervalHours),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
