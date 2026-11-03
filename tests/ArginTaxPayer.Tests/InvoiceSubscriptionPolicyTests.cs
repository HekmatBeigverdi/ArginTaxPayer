using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class InvoiceSubscriptionPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Active_plan_allows_send_but_exact_expiry_blocks_it(int kind)
    {
        Assert.Null(InvoiceSubscriptionPolicy.BlockingCode((SubscriptionPlanKind)kind,
            SubscriptionLifecycle.Active, Now.AddDays(-1), Now.AddTicks(1), 1, Now));
        Assert.Equal("subscription_expired", InvoiceSubscriptionPolicy.BlockingCode((SubscriptionPlanKind)kind,
            SubscriptionLifecycle.Active, Now.AddDays(-1), Now, 100, Now));
    }

    [Theory]
    [InlineData(null)] [InlineData(0L)] [InlineData(-1L)]
    public void Quantity_plan_without_available_units_is_blocked(long? available)
    {
        Assert.Equal("subscription_exhausted", InvoiceSubscriptionPolicy.BlockingCode(SubscriptionPlanKind.Quantity,
            SubscriptionLifecycle.Active, Now.AddDays(-1), Now.AddDays(1), available, Now));
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Timed_plan_does_not_need_invoice_credit(int kind)
    {
        Assert.Null(InvoiceSubscriptionPolicy.BlockingCode((SubscriptionPlanKind)kind,
            SubscriptionLifecycle.Active, Now.AddDays(-1), Now.AddDays(1), null, Now));
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(4)]
    public void Inactive_subscription_does_not_authorize_send(int state)
    {
        Assert.NotNull(InvoiceSubscriptionPolicy.BlockingCode(SubscriptionPlanKind.Quantity,
            (SubscriptionLifecycle)state, Now.AddDays(-1), Now.AddDays(1), 10, Now));
    }

    [Fact]
    public void Future_activation_does_not_authorize_send()
    {
        Assert.NotNull(InvoiceSubscriptionPolicy.BlockingCode(SubscriptionPlanKind.Quantity,
            SubscriptionLifecycle.Active, Now.AddTicks(1), Now.AddDays(1), 10, Now));
    }
}
