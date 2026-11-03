using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12CreditLedgerPolicyTests
{
    [Fact]
    public void Reserve_consume_release_conserve_quantity()
    {
        var start = new SubscriptionCreditState(50,50,0,0);
        var afterReserve = start.Transition(SubscriptionCreditAction.Reserve,5,0);
        Assert.Equal(new SubscriptionCreditState(50,45,5,0),afterReserve);
        var afterConsume = afterReserve.Transition(SubscriptionCreditAction.Consume,3,5);
        Assert.Equal(new SubscriptionCreditState(50,45,2,3),afterConsume);
        var afterRelease = afterConsume.Transition(SubscriptionCreditAction.Release,2,2);
        Assert.Equal(new SubscriptionCreditState(50,47,0,3),afterRelease);
    }

    [Fact]
    public void Denies_over_reservation_and_over_consumption()
    {
        var state = new SubscriptionCreditState(100,10,5,85);
        Assert.Throws<InvalidOperationException>(() =>
            state.Transition(SubscriptionCreditAction.Reserve,11,0));
        Assert.Throws<InvalidOperationException>(() =>
            state.Transition(SubscriptionCreditAction.Consume,6,5));
        Assert.Throws<InvalidOperationException>(() =>
            state.Transition(SubscriptionCreditAction.Release,5,4));
    }

    [Fact]
    public void Denies_negative_or_nonconserving_balances()
    {
        Assert.Throws<InvalidOperationException>(() => new SubscriptionCreditState(50,40,10,1).Validate());
        Assert.Throws<InvalidOperationException>(() => new SubscriptionCreditState(50,-1,0,51).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SubscriptionCreditState(10,10,0,0).Transition(SubscriptionCreditAction.Reserve,0,0));
    }
}
