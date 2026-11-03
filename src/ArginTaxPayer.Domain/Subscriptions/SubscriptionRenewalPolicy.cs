namespace ArginTaxPayer.Domain.Subscriptions;
public enum SubscriptionPurchasePurpose { New = 1, Renewal = 2, RepeatQuantity = 3, AddonQuantity = 4 }
public sealed record SubscriptionPurchaseWindow(SubscriptionPurchasePurpose Purpose,
    SubscriptionPlanKind Kind, DateTime ActivatedUtc, DateTime ExpiresUtc,
    DateTime? ExistingExpiresUtc)
{
    public void Validate()
    {
        if(ActivatedUtc.Kind!=DateTimeKind.Utc || ExpiresUtc.Kind!=DateTimeKind.Utc ||
           ExpiresUtc<=ActivatedUtc || (ExistingExpiresUtc is not null &&
           ExistingExpiresUtc.Value.Kind!=DateTimeKind.Utc))
            throw new ArgumentException("Invalid subscription dates.");
        if(Purpose==SubscriptionPurchasePurpose.AddonQuantity)
        {
            if(Kind!=SubscriptionPlanKind.Quantity || ExistingExpiresUtc is null ||
                ExistingExpiresUtc<=ActivatedUtc || ExpiresUtc!=ExistingExpiresUtc)
                throw new InvalidOperationException("An add-on must inherit the active quantity subscription expiry.");
        }
        if(Purpose==SubscriptionPurchasePurpose.RepeatQuantity && Kind!=SubscriptionPlanKind.Quantity)
            throw new InvalidOperationException("Repeat quantity purchases require a quantity plan.");
    }
    public static DateTime ResolveExpiry(SubscriptionPurchasePurpose purpose,
        SubscriptionPlanKind kind,DateTime startUtc,DateTime? baseExpiryUtc,int? quantityMonths)
    {
        if(purpose==SubscriptionPurchasePurpose.AddonQuantity)
        {
            if(kind!=SubscriptionPlanKind.Quantity || baseExpiryUtc is null ||
               baseExpiryUtc.Value.Kind!=DateTimeKind.Utc || baseExpiryUtc<=startUtc)
                throw new InvalidOperationException("No active quantity subscription to extend with an add-on.");
            return baseExpiryUtc.Value;
        }
        return SubscriptionCalendar.ExpirationUtc(kind,startUtc,quantityMonths);
    }
}
