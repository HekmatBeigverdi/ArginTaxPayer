namespace ArginTaxPayer.Domain.Subscriptions;

/// <summary>Commercial durations are calendar months, not fixed 30-day blocks.
/// DateTime values must be UTC. End-of-month anchors stay at month end.</summary>
public static class SubscriptionCalendar
{
    public static int Months(SubscriptionPlanKind kind) => kind switch
    {
        SubscriptionPlanKind.MonthlySingleTaxpayer => 1,
        SubscriptionPlanKind.QuarterlySingleTaxpayer => 3,
        SubscriptionPlanKind.SemiAnnualSingleTaxpayer => 6,
        SubscriptionPlanKind.AnnualSingleTaxpayer => 12,
        _ => throw new ArgumentException("Quantity packages require an explicit expiry.", nameof(kind))
    };

    public static DateTime ExpirationUtc(SubscriptionPlanKind kind, DateTime activatedAtUtc,
        int? quantityMonths = null)
    {
        if (activatedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Activation must be in UTC.", nameof(activatedAtUtc));
        int months;
        if (kind == SubscriptionPlanKind.Quantity)
        {
            if (quantityMonths is < 1 or > 12 || quantityMonths is null)
                throw new ArgumentOutOfRangeException(nameof(quantityMonths));
            months = quantityMonths.Value;
        }
        else
        {
            if (quantityMonths is not null) throw new ArgumentException("Duration is fixed by plan kind.");
            months = Months(kind);
        }
        var target = activatedAtUtc.AddMonths(months);
        if (activatedAtUtc.Day == DateTime.DaysInMonth(activatedAtUtc.Year, activatedAtUtc.Month))
            target = new DateTime(target.Year, target.Month,
                DateTime.DaysInMonth(target.Year, target.Month),
                target.Hour, target.Minute, target.Second, target.Millisecond, DateTimeKind.Utc)
                .AddTicks(target.Ticks % TimeSpan.TicksPerMillisecond);
        return target;
    }
}
