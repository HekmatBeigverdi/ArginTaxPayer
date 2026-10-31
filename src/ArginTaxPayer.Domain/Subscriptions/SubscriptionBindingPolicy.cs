namespace ArginTaxPayer.Domain.Subscriptions;

public enum BindingExceptionStatus { Pending = 1, Approved = 2, Rejected = 3 }

public sealed record AnnualSubscriptionBinding(Guid SubscriptionId, Guid CustomerUserId, Guid TaxpayerId,
    Guid FiscalMemoryId, DateTime VerifiedConnectedAtUtc)
{
    public void Validate()
    {
        if (SubscriptionId == Guid.Empty || CustomerUserId == Guid.Empty ||
            TaxpayerId == Guid.Empty || FiscalMemoryId == Guid.Empty ||
            VerifiedConnectedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Single-taxpayer binding requires complete IDs and UTC registration.");
    }

    public bool Matches(Guid customerId, Guid taxpayerId, Guid fiscalMemoryId) =>
        CustomerUserId == customerId && TaxpayerId == taxpayerId && FiscalMemoryId == fiscalMemoryId;
}

public static class SubscriptionBindingPolicy
{
    public static bool IsSingleTaxpayer(SubscriptionPlanKind kind) => kind is SubscriptionPlanKind.AnnualSingleTaxpayer or SubscriptionPlanKind.MonthlySingleTaxpayer;

    public static void RequireEligible(SubscriptionPlanKind kind, SubscriptionLifecycle state,
        DateTime? activatedUtc, DateTime? expiresUtc, DateTime atUtc)
    {
        if (!Enum.IsDefined(kind) || state != SubscriptionLifecycle.Active ||
            activatedUtc is null || expiresUtc is null ||
            activatedUtc.Value.Kind != DateTimeKind.Utc || expiresUtc.Value.Kind != DateTimeKind.Utc ||
            atUtc.Kind != DateTimeKind.Utc || atUtc < activatedUtc || atUtc >= expiresUtc)
            throw new InvalidOperationException("Subscription not active and valid.");
    }

    public static void RequireAnnualMatch(AnnualSubscriptionBinding? binding,
        Guid customerId, Guid taxpayerId, Guid memoryId)
    {
        if (binding is null || !binding.Matches(customerId, taxpayerId, memoryId))
            throw new InvalidOperationException("Single-taxpayer subscription is restricted to its selected taxpayer and memory.");
    }
}
