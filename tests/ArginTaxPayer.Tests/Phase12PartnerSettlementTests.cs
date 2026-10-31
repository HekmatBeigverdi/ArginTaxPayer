using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12PartnerSettlementTests
{
    [Fact]
    public void Partial_and_full_settlement_are_conserved()
    {
        var initial=new PartnerSettlementPosition(1_000_000,0,0);
        var partial=initial.Pay(300_000);
        Assert.Equal(700_000,partial.OutstandingIrr);
        var fully=partial.Pay(700_000);
        Assert.Equal(0,fully.OutstandingIrr);
        Assert.Throws<InvalidOperationException>(()=>fully.Pay(1));
    }
    [Fact]
    public void Reversal_restores_payable_balance_without_erase()
    {
        var recorded=new PartnerSettlementPosition(500_000,500_000,0);
        var corrected=recorded.Reverse(200_000);
        Assert.Equal(300_000,corrected.NetSettledIrr);
        Assert.Equal(200_000,corrected.OutstandingIrr);
        Assert.Throws<InvalidOperationException>(()=>corrected.Reverse(400_000));
    }
    [Fact]
    public void Invalid_values_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(()=>
            new PartnerSettlementPosition(100,110,0).Validate());
        Assert.Throws<InvalidOperationException>(()=>
            new PartnerSettlementPosition(100,10,11).Validate());
        Assert.Throws<InvalidOperationException>(()=>
            new PartnerSettlementPosition(100,0,0).Pay(101));
    }
}
