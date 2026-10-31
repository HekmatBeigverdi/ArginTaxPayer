using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12PartnerRevenuePolicyTests
{
    [Fact]
    public void Partner_share_is_based_on_verified_cash_after_root_approved_cost()
    {
        var result=PartnerRevenuePolicy.Calculate(1_000_000,100_000,new PartnerShareTerms(3000));
        Assert.Equal(900_000,result.NetDistributableIrr);
        Assert.Equal(270_000,result.PartnerShareIrr);
        Assert.Equal(630_000,result.RootShareIrr);
        Assert.Equal(result.CollectedIrr,result.ApprovedCostIrr+result.PartnerShareIrr+result.RootShareIrr);
    }

    [Fact]
    public void Direct_root_collections_have_no_partner_share()
    {
        var result=PartnerRevenuePolicy.Calculate(1_000_000,0,new PartnerShareTerms(0));
        Assert.Equal(0,result.PartnerShareIrr);
        Assert.Equal(1_000_000,result.RootShareIrr);
    }

    [Fact]
    public void Unapproved_or_excessive_cost_cannot_be_allocated()
    {
        Assert.Throws<InvalidOperationException>(()=>
            PartnerRevenuePolicy.Calculate(100,101,new PartnerShareTerms(2000)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>
            PartnerRevenuePolicy.Calculate(100,0,new PartnerShareTerms(10001)));
    }
}
