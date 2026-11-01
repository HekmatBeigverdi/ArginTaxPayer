using ArginTaxPayer.Domain.Subscriptions;
using Xunit;
namespace ArginTaxPayer.Tests;
public sealed class Phase12RenewalPurchasePolicyTests
{
    [Fact] public void Addon_inherits_existing_quantity_expiry()
    {
        var now=new DateTime(2026,11,1,0,0,0,DateTimeKind.Utc);
        var oldExpiry=now.AddDays(27);
        var newExpiry=SubscriptionPurchaseWindow.ResolveExpiry(
            SubscriptionPurchasePurpose.AddonQuantity,
            SubscriptionPlanKind.Quantity,now,oldExpiry,null);
        Assert.Equal(oldExpiry,newExpiry);
        new SubscriptionPurchaseWindow(SubscriptionPurchasePurpose.AddonQuantity,
            SubscriptionPlanKind.Quantity,now,newExpiry,oldExpiry).Validate();
    }
    [Fact] public void New_quantity_purchase_can_have_own_validity()
    {
        var now=new DateTime(2026,11,1,0,0,0,DateTimeKind.Utc);
        var expires=SubscriptionPurchaseWindow.ResolveExpiry(
            SubscriptionPurchasePurpose.RepeatQuantity,
            SubscriptionPlanKind.Quantity,now,null,3);
        Assert.Equal(now.AddMonths(3),expires);
    }
    [Fact] public void Addon_cannot_extend_or_skip_expiry()
    {
        var now=new DateTime(2026,11,1,0,0,0,DateTimeKind.Utc);
        Assert.Throws<InvalidOperationException>(()=>
            SubscriptionPurchaseWindow.ResolveExpiry(SubscriptionPurchasePurpose.AddonQuantity,
                SubscriptionPlanKind.Quantity,now,null,null));
        Assert.Throws<InvalidOperationException>(()=>
            new SubscriptionPurchaseWindow(SubscriptionPurchasePurpose.AddonQuantity,
                SubscriptionPlanKind.Quantity,now,now.AddMonths(12),now.AddMonths(1)).Validate());
    }
}
