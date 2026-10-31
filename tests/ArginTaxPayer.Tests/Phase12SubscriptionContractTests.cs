using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12SubscriptionContractTests
{
    [Fact]
    public void Annual_is_unlimited_and_quantity_requires_units()
    {
        new SubscriptionPlanTerms(Guid.NewGuid(), SubscriptionPlanKind.AnnualSingleTaxpayer, 365, null).Validate();
        Assert.Throws<ArgumentException>(() => new SubscriptionPlanTerms(Guid.NewGuid(), SubscriptionPlanKind.AnnualSingleTaxpayer, 365, 1).Validate());
        Assert.Throws<ArgumentException>(() => new SubscriptionPlanTerms(Guid.NewGuid(), SubscriptionPlanKind.Quantity, 365, null).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SubscriptionPlanTerms(Guid.NewGuid(), SubscriptionPlanKind.Quantity, 367, 3).Validate());
    }

    [Fact]
    public void Partner_price_requires_a_partner_and_exact_time_window()
    {
        var now = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var p = new SubscriptionPriceVersion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PriceAudience.Partner, 1000, now, now.AddDays(1));
        p.Validate();
        Assert.True(p.AppliesAt(now));
        Assert.False(p.AppliesAt(now.AddDays(1)));
        Assert.Throws<ArgumentException>(() => (p with { PartnerUserId = null }).Validate());
    }

    [Fact]
    public void Purchase_snapshots_terms_and_price()
    {
        var now = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var terms = new SubscriptionPlanTerms(Guid.NewGuid(), SubscriptionPlanKind.Quantity, 365, 100);
        var price = new SubscriptionPriceVersion(Guid.NewGuid(), terms.PlanId, Guid.NewGuid(), null, PriceAudience.RootDirect, 900000, now, null);
        var snapshot = SubscriptionPurchaseSnapshot.Create(Guid.NewGuid(), terms, price, now);
        Assert.Equal(100L, snapshot.InvoiceUnitLimit);
        Assert.Equal(900000L, snapshot.PriceIrr);
        Assert.Null(snapshot.PartnerUserId);
        Assert.Throws<ArgumentException>(() => SubscriptionPurchaseSnapshot.Create(Guid.NewGuid(), terms, price, now.AddDays(-1)));
    }
}
