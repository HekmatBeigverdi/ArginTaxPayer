using ArginTaxPayer.Api.Dashboard;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase15DashboardContractTests
{
    [Theory]
    [InlineData("SUCCESS",5,"accepted")]
    [InlineData("REJECTED",5,"rejected")]
    [InlineData("FAILED",5,"rejected")]
    [InlineData(null,5,"awaitingInquiry")]
    [InlineData(null,6,"unknown")]
    [InlineData(null,4,"processing")]
    [InlineData(null,1,"prepared")]
    [InlineData(null,2,"prepared")]
    [InlineData(null,3,"prepared")]
    [InlineData(null,7,"attention")]
    [InlineData(null,8,"attention")]
    [InlineData("NOT_AN_OFFICIAL_STATUS",5,"unclassified")]
    public void Authority_and_dispatch_evidence_are_distinct(string? report,int state,string expected)
        => Assert.Equal(expected,Phase15DashboardReadService.Classify(report,state));

    [Fact]
    public void Tehran_date_range_is_half_open_and_not_UTC_midnight()
    {
        var period=Phase15DashboardReadService.ResolveWindow("custom","2026-11-01","2026-11-02",
            new DateTime(2026,11,3,8,0,0,DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026,10,31,20,30,0,DateTimeKind.Utc),period.FromUtc);
        Assert.Equal(new DateTime(2026,11,2,20,30,0,DateTimeKind.Utc),period.ToUtcExclusive);
    }

    [Fact]
    public void Invalid_periods_and_future_dates_are_rejected()
    {
        var now=new DateTime(2026,11,3,8,0,0,DateTimeKind.Utc);
        Assert.Throws<ArgumentException>(()=>Phase15DashboardReadService.ResolveWindow("all",null,null,now));
        Assert.Throws<ArgumentException>(()=>Phase15DashboardReadService.ResolveWindow("30d","2026-11-01",null,now));
        Assert.Throws<ArgumentException>(()=>Phase15DashboardReadService.ResolveWindow("custom","2026-12-01","2026-12-02",now));
        Assert.Throws<ArgumentException>(()=>Phase15DashboardReadService.ResolveWindow("custom","2026-11-02","2026-11-01",now));
    }
}
