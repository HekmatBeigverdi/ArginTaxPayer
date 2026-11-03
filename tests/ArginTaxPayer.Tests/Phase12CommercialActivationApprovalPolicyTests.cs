using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12CommercialActivationApprovalPolicyTests
{
    [Theory]
    [InlineData(1_000_000,0)]
    [InlineData(1_000_000,200_000)]
    [InlineData(1_000_000,1_000_000)]
    public void Administrator_may_activate_with_unpaid_partial_or_full_balance(long gross,long collected)
    {
        CommercialActivationApprovalPolicy.RequireApprovable(
            gross,collected,CommercialOrderStatus.Pending);
        Assert.Equal(gross-collected,
            CommercialActivationApprovalPolicy.OutstandingIrr(gross,collected));
    }

    [Fact]
    public void Voided_order_cannot_be_activated_even_if_paid()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CommercialActivationApprovalPolicy.RequireApprovable(
                1_000_000,1_000_000,CommercialOrderStatus.Voided));
    }

    [Theory]
    [InlineData(0,0)]
    [InlineData(100,101)]
    [InlineData(100,-1)]
    public void Invalid_financial_positions_cannot_be_approved(long gross,long collected)
    {
        Assert.Throws<InvalidOperationException>(() =>
            CommercialActivationApprovalPolicy.RequireApprovable(
                gross,collected,CommercialOrderStatus.Pending));
    }
}
