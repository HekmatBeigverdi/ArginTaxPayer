using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12PlanSalesReportingTests
{
    [Theory]
    [InlineData("month", 2026, 11, 2, 2026, 10, 22)]
    [InlineData("year", 2026, 11, 2, 2026, 3, 20)]
    [InlineData("month", 2026, 3, 21, 2026, 3, 20)]
    [InlineData("year", 2026, 3, 21, 2026, 3, 20)]
    public void Period_starts_at_Persian_calendar_boundary_in_Tehran(
        string period,int y,int m,int d,int sy,int sm,int sd)
    {
        var now=new DateTime(y,m,d,12,0,0,DateTimeKind.Utc);
        var range=CommercialReportPeriod.Resolve(period,now);
        Assert.Equal(new DateTime(sy,sm,sd,20,30,0,DateTimeKind.Utc),range.FromUtc);
        Assert.Equal(now,range.ThroughUtc);
    }

    [Fact]
    public void Tehran_midnight_moves_to_next_Persian_month_before_UTC_midnight()
    {
        var range=CommercialReportPeriod.Resolve("month",new DateTime(2026,10,22,21,0,0,DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026,10,22,20,30,0,DateTimeKind.Utc),range.FromUtc);
    }

    [Fact]
    public void Invalid_period_and_non_UTC_clock_are_rejected()
    {
        Assert.Throws<ArgumentException>(()=>CommercialReportPeriod.Resolve("week",DateTime.UtcNow));
        Assert.Throws<ArgumentException>(()=>CommercialReportPeriod.Resolve("month",new DateTime(2026,11,2)));
    }

    [Theory]
    [InlineData(1,0)]
    [InlineData(2,10)]
    [InlineData(3,20)]
    public void Database_page_offsets_are_ten_rows(int page,int expected) =>
        Assert.Equal(expected,SubscriptionPaging.Offset(page));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Invalid_or_overflowing_database_pages_are_rejected(int page) =>
        Assert.Throws<ArgumentOutOfRangeException>(()=>SubscriptionPaging.Offset(page));
}
