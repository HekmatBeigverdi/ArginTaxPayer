using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12PartnerLedgerReportingTests
{
    [Fact]
    public void Paged_ledger_reports_full_scoped_totals_without_double_counting()
    {
        var root=Guid.NewGuid();
        var partner=Guid.NewGuid();
        var stamp=new DateTime(2026,11,1,12,0,0,DateTimeKind.Utc);
        var items=new [] {
            new PartnerRevenueLedgerRow(Guid.NewGuid(),Guid.NewGuid(),root,partner,1000,100,270,630,stamp),
            new PartnerRevenueLedgerRow(Guid.NewGuid(),Guid.NewGuid(),root,partner,2000,0,600,1400,stamp.AddMinutes(1))
        };
        var first=PartnerRevenueLedgerReadService.ComposePage(items,1,1);
        var second=PartnerRevenueLedgerReadService.ComposePage(items,2,1);
        Assert.Single(first.Items);
        Assert.Single(second.Items);
        Assert.Equal(3000,first.TotalGrossIrr);
        Assert.Equal(870,first.TotalPartnerShareIrr);
        Assert.Equal(2030,first.TotalRootShareIrr);
        Assert.Equal(3000,second.TotalGrossIrr);
        Assert.Equal(items[1].AllocationId,first.Items[0].AllocationId);
    }

    [Fact]
    public void Rejects_invalid_pagination()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>
            PartnerRevenueLedgerReadService.ComposePage([],0,15));
        Assert.Throws<ArgumentOutOfRangeException>(()=>
            PartnerRevenueLedgerReadService.ComposePage([],1,101));
    }
}
