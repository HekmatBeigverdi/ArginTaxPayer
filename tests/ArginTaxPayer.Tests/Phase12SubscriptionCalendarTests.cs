using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;
public sealed class Phase12SubscriptionCalendarTests
{
    [Theory]
    [InlineData(SubscriptionPlanKind.MonthlySingleTaxpayer, 2026, 1, 31, 2026, 2, 28)]
    [InlineData(SubscriptionPlanKind.QuarterlySingleTaxpayer, 2026, 1, 31, 2026, 4, 30)]
    [InlineData(SubscriptionPlanKind.SemiAnnualSingleTaxpayer, 2026, 8, 31, 2027, 2, 28)]
    [InlineData(SubscriptionPlanKind.AnnualSingleTaxpayer, 2024, 2, 29, 2025, 2, 28)]
    public void Terms_use_calendar_anniversaries(SubscriptionPlanKind kind, int y, int m, int d,
        int expectedY, int expectedM, int expectedD)
    {
        var start = new DateTime(y,m,d,12,30,0,DateTimeKind.Utc);
        var expiry = SubscriptionCalendar.ExpirationUtc(kind, start);
        Assert.Equal(new DateTime(expectedY,expectedM,expectedD,12,30,0,DateTimeKind.Utc), expiry);
    }

    [Fact]
    public void Midmonth_uses_anniversary_day_and_keeps_time()
    {
        var from = new DateTime(2026, 1, 15, 23, 59, 58, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 4, 15, 23, 59, 58, DateTimeKind.Utc),
            SubscriptionCalendar.ExpirationUtc(SubscriptionPlanKind.QuarterlySingleTaxpayer, from));
    }

    [Fact]
    public void Rejects_wrong_duration_and_non_utc()
    {
        Assert.Throws<ArgumentException>(() =>
            SubscriptionCalendar.ExpirationUtc(SubscriptionPlanKind.MonthlySingleTaxpayer,
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() =>
            SubscriptionCalendar.ExpirationUtc(SubscriptionPlanKind.AnnualSingleTaxpayer, DateTime.UtcNow, 12));
    }

    [Fact]
    public void Quantity_never_exceeds_twelve_months()
    {
        var now = new DateTime(2026,11,30,10,0,0,DateTimeKind.Utc);
        Assert.Equal(new DateTime(2027,11,30,10,0,0,DateTimeKind.Utc),
            SubscriptionCalendar.ExpirationUtc(SubscriptionPlanKind.Quantity,now,12));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SubscriptionCalendar.ExpirationUtc(SubscriptionPlanKind.Quantity,now,13));
    }
}
