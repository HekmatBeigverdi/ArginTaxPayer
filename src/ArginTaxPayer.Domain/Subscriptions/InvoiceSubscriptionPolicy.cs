namespace ArginTaxPayer.Domain.Subscriptions;

public static class InvoiceSubscriptionPolicy
{
    public static string? BlockingCode(SubscriptionPlanKind kind, SubscriptionLifecycle state,
        DateTime? activatedAtUtc, DateTime? expiresAtUtc, long? availableUnits, DateTime nowUtc)
    {
        if (!Enum.IsDefined(kind) || state != SubscriptionLifecycle.Active ||
            activatedAtUtc is null || expiresAtUtc is null || activatedAtUtc > nowUtc)
            return "subscription_required";
        if (nowUtc >= expiresAtUtc) return "subscription_expired";
        return kind == SubscriptionPlanKind.Quantity && availableUnits is not > 0
            ? "subscription_exhausted" : null;
    }
}
