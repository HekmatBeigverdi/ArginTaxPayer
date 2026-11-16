using ArginTaxPayer.Domain.Administration;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed class DatabaseBackupScheduler
{
    public static DateTime GetNextRunUtc(DateTime nowUtc, DatabaseBackupScheduleKind kind, int intervalHours)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Scheduler clock must be UTC.", nameof(nowUtc));
        if (intervalHours is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(intervalHours));
        return kind switch
        {
            DatabaseBackupScheduleKind.Daily => nowUtc.Date.AddDays(1),
            DatabaseBackupScheduleKind.IntervalHours => nowUtc.Date.AddHours(
                (Math.Floor(nowUtc.TimeOfDay.TotalHours / intervalHours) + 1) * intervalHours),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
