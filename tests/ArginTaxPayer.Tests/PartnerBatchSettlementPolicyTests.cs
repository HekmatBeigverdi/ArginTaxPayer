using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PartnerBatchSettlementPolicyTests
{
    [Theory]
    [InlineData(1000,0,0,3000,300,700,700)]
    [InlineData(1000,200,0,3000,240,560,560)]
    [InlineData(1000,200,200,3000,240,560,760)]
    [InlineData(1000,200,100,3000,240,560,660)]
    [InlineData(101,0,0,3333,33,68,68)]
    [InlineData(1000,1000,1000,3000,0,0,1000)]
    [InlineData(1000,0,0,0,0,1000,1000)]
    [InlineData(1000,0,0,10000,1000,0,0)]
    public void Cash_is_split_after_cost_and_Level1_is_reimbursed_only_for_cost_it_paid(
        long gross,long cost,long rootCost,int rate,long partnerShare,long rootShare,long net)
    {
        var value=PartnerBatchSettlementPolicy.Calculate(gross,cost,rootCost,rate);
        Assert.Equal(partnerShare,value.PartnerShareIrr);
        Assert.Equal(rootShare,value.RootShareIrr);
        Assert.Equal(net,value.NetToRootIrr);
        Assert.Equal(gross,value.PartnerShareIrr+value.RootShareIrr+cost);
    }

    [Fact]
    public void Later_cost_produces_only_incremental_adjustment()
    {
        var first=PartnerBatchSettlementPolicy.Calculate(1000,0,0,3000);
        var rootExpense=PartnerBatchSettlementPolicy.Calculate(1000,200,200,3000);
        var partnerExpense=PartnerBatchSettlementPolicy.Calculate(1000,200,0,3000);
        Assert.Equal(60,rootExpense.NetToRootIrr-first.NetToRootIrr);
        Assert.Equal(-140,partnerExpense.NetToRootIrr-first.NetToRootIrr);
    }

    [Theory]
    [InlineData(-1,0,0,3000)]
    [InlineData(100,-1,0,3000)]
    [InlineData(100,101,0,3000)]
    [InlineData(100,50,51,3000)]
    [InlineData(100,0,-1,3000)]
    [InlineData(100,0,0,-1)]
    [InlineData(100,0,0,10001)]
    public void Invalid_cash_cost_and_rate_are_rejected(long gross,long cost,long rootCost,int rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(()=>PartnerBatchSettlementPolicy.Calculate(gross,cost,rootCost,rate));
}
