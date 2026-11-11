using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12SubscriptionBindingPolicyTests
{
    [Fact]
    public void Active_valid_window_accepts_beginning_and_rejects_expiry()
    {
        var start = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        SubscriptionBindingPolicy.RequireEligible(SubscriptionPlanKind.AnnualSingleTaxpayer,
            SubscriptionLifecycle.Active, start, start.AddDays(365), start);
        Assert.Throws<InvalidOperationException>(() =>
            SubscriptionBindingPolicy.RequireEligible(SubscriptionPlanKind.AnnualSingleTaxpayer,
                SubscriptionLifecycle.Active, start, start.AddDays(365), start.AddDays(365)));
        Assert.Throws<InvalidOperationException>(() =>
            SubscriptionBindingPolicy.RequireEligible(SubscriptionPlanKind.AnnualSingleTaxpayer,
                SubscriptionLifecycle.PendingPayment, start, start.AddDays(365), start));
    }

    [Fact]
    public void Annual_binding_is_fixed_to_customer_taxpayer_and_memory()
    {
        var customer = Guid.NewGuid();
        var taxpayer = Guid.NewGuid();
        var memory = Guid.NewGuid();
        var binding = new AnnualSubscriptionBinding(Guid.NewGuid(), customer, taxpayer,
            memory, DateTime.UtcNow);
        binding.Validate();
        SubscriptionBindingPolicy.RequireAnnualMatch(binding, customer, taxpayer, memory);
        Assert.Throws<InvalidOperationException>(() =>
            SubscriptionBindingPolicy.RequireAnnualMatch(binding, customer, taxpayer, Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() =>
            SubscriptionBindingPolicy.RequireAnnualMatch(binding, customer, Guid.NewGuid(), memory));
        Assert.Throws<InvalidOperationException>(() =>
            SubscriptionBindingPolicy.RequireAnnualMatch(null, customer, taxpayer, memory));
    }

    [Fact]
    public void Unverified_binding_fails_validation()
    {
        Assert.Throws<ArgumentException>(() => new AnnualSubscriptionBinding(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.MinValue).Validate());
    }
}
