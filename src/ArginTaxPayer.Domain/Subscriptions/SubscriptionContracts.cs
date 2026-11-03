namespace ArginTaxPayer.Domain.Subscriptions;

public enum SubscriptionPlanKind { AnnualSingleTaxpayer = 1, Quantity = 2, MonthlySingleTaxpayer = 3, QuarterlySingleTaxpayer = 4, SemiAnnualSingleTaxpayer = 5 }
public enum SubscriptionLifecycle { PendingPayment = 1, Active = 2, Expired = 3, Cancelled = 4 }
public enum PriceAudience { RootDirect = 1, Partner = 2 }

/// <summary>Immutable contractual terms; payment and entitlements are handled in later steps.</summary>
public sealed record SubscriptionPlanTerms(
    Guid PlanId, SubscriptionPlanKind Kind, int ValidityDays, long? InvoiceUnitLimit)
{
    public void Validate()
    {
        if (PlanId == Guid.Empty) throw new ArgumentException("Plan identifier required.");
        if (ValidityDays is <= 0 or > 366) throw new ArgumentOutOfRangeException(nameof(ValidityDays));
        if (Kind is SubscriptionPlanKind.AnnualSingleTaxpayer or SubscriptionPlanKind.MonthlySingleTaxpayer or SubscriptionPlanKind.QuarterlySingleTaxpayer or SubscriptionPlanKind.SemiAnnualSingleTaxpayer)
        {
            if (InvoiceUnitLimit is not null) throw new ArgumentException("Single-taxpayer plans do not count invoices.");
            if (Kind == SubscriptionPlanKind.MonthlySingleTaxpayer && ValidityDays is < 28 or > 31)
                throw new ArgumentException("Invalid monthly plan duration.");
            if (Kind == SubscriptionPlanKind.QuarterlySingleTaxpayer && ValidityDays is < 89 or > 92)
                throw new ArgumentException("Invalid quarterly plan duration.");
            if (Kind == SubscriptionPlanKind.SemiAnnualSingleTaxpayer && ValidityDays is < 181 or > 184)
                throw new ArgumentException("Invalid six-month plan duration.");
            if (Kind == SubscriptionPlanKind.AnnualSingleTaxpayer && ValidityDays is < 365 or > 366)
                throw new ArgumentException("Invalid annual plan duration.");
        }
        if (Kind == SubscriptionPlanKind.Quantity && InvoiceUnitLimit is not > 0)
            throw new ArgumentException("Quantity plans require a positive unit limit.");
        if (!Enum.IsDefined(Kind)) throw new ArgumentException("Unsupported plan kind.");
    }
}

/// <summary>Immutable purchase-time price quote. All amounts in IRR.</summary>
public sealed record SubscriptionPriceVersion(
    Guid Id, Guid PlanId, Guid RootAdminUserId, Guid? PartnerUserId,
    PriceAudience Audience, long PriceIrr, DateTime EffectiveFromUtc, DateTime? EffectiveUntilUtc)
{
    public void Validate()
    {
        if (Id == Guid.Empty || PlanId == Guid.Empty || RootAdminUserId == Guid.Empty)
            throw new ArgumentException("Price/plan/root identifiers required.");
        if (PriceIrr < 0) throw new ArgumentOutOfRangeException(nameof(PriceIrr));
        if (EffectiveFromUtc.Kind != DateTimeKind.Utc ||
            (EffectiveUntilUtc.HasValue && EffectiveUntilUtc.Value.Kind != DateTimeKind.Utc))
            throw new ArgumentException("Price windows must use UTC.");
        if (EffectiveUntilUtc <= EffectiveFromUtc)
            throw new ArgumentException("Price window must be positive.");
        if (!Enum.IsDefined(Audience) ||
            (Audience == PriceAudience.Partner) != PartnerUserId.HasValue)
            throw new ArgumentException("Audience and partner ownership mismatch.");
        if (PartnerUserId == Guid.Empty)
            throw new ArgumentException("Partner identifier cannot be empty.");
    }

    public bool AppliesAt(DateTime atUtc) =>
        atUtc.Kind == DateTimeKind.Utc && atUtc >= EffectiveFromUtc &&
        (EffectiveUntilUtc is null || atUtc < EffectiveUntilUtc);
}

/// <summary>Purchase snapshot; transitions and payment verification are intentionally not enabled in Step 02.</summary>
public sealed record SubscriptionPurchaseSnapshot(
    Guid CustomerUserId, Guid PlanId, Guid PriceVersionId,
    SubscriptionPlanKind Kind, int ValidityDays, long? InvoiceUnitLimit,
    long PriceIrr, Guid RootAdminUserId, Guid? PartnerUserId, DateTime PurchasedAtUtc)
{
    public static SubscriptionPurchaseSnapshot Create(
        Guid customerUserId, SubscriptionPlanTerms terms,
        SubscriptionPriceVersion price, DateTime purchasedAtUtc)
    {
        terms.Validate();
        price.Validate();
        if (customerUserId == Guid.Empty || price.PlanId != terms.PlanId ||
            !price.AppliesAt(purchasedAtUtc))
            throw new ArgumentException("Invalid purchase price or customer.");
        return new(customerUserId, terms.PlanId, price.Id, terms.Kind, terms.ValidityDays,
            terms.InvoiceUnitLimit, price.PriceIrr, price.RootAdminUserId,
            price.PartnerUserId, purchasedAtUtc);
    }
}
