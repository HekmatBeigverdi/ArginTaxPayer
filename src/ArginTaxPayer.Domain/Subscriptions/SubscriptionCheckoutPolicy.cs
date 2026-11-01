namespace ArginTaxPayer.Domain.Subscriptions;

public enum SubscriptionPaymentMethod { CashPendingApproval = 1, Gateway = 2 }
public enum SubscriptionCheckoutState
{
    AwaitingCashPayment = 1, AwaitingAdminApproval = 2,
    AwaitingGatewayPayment = 3, GatewayUnverified = 4,
    PaidVerified = 5, Activated = 6, Rejected = 7
}
public enum SubscriptionGateway { Sep = 1, Zarinpal = 2 }

/// <summary>Purchase amount is snapshotted once. An internal software sales tax invoice
/// is distinct from an invoice submitted by a taxpayer to the tax authority.</summary>
public sealed record SubscriptionCheckoutQuote(
    long BaseAmountIrr, int VatBasisPoints, bool WantsOfficialInvoice)
{
    public long TaxAmountIrr
    {
        get
        {
            Validate();
            // Mathematical rounding to whole IRR, half-up for positive amounts.
            return WantsOfficialInvoice
                ? checked((BaseAmountIrr * VatBasisPoints + 5000L) / 10000L) : 0;
        }
    }
    public long TotalIrr => checked(BaseAmountIrr + TaxAmountIrr);
    public void Validate()
    {
        if(BaseAmountIrr < 0 || VatBasisPoints is < 0 or > 10000)
            throw new ArgumentOutOfRangeException("Invalid checkout VAT or price.");
    }
}
public sealed record GatewayMerchantConfiguration(
    Guid OwnerAdminId, SubscriptionGateway Provider, bool ForOfficialInvoice,
    string TerminalAlias, bool Enabled)
{
    public void Validate()
    {
        if(OwnerAdminId==Guid.Empty || !Enum.IsDefined(Provider) ||
            string.IsNullOrWhiteSpace(TerminalAlias) || TerminalAlias.Length>80)
            throw new ArgumentException("Invalid merchant binding.");
    }
}
public static class SubscriptionGatewaySelection
{
    public static GatewayMerchantConfiguration Select(
        IEnumerable<GatewayMerchantConfiguration> enabledForAdmin,
        Guid ownerAdminId, bool officialInvoice)
    {
        var matches=enabledForAdmin
            .Where(x=>x.OwnerAdminId==ownerAdminId &&
                x.ForOfficialInvoice==officialInvoice && x.Enabled)
            .OrderBy(x=>x.Provider==SubscriptionGateway.Sep?0:1)
            .ToArray();
        if(matches.Length==0)
            throw new InvalidOperationException("No gateway configured for selected invoice mode.");
        foreach(var merchant in matches) merchant.Validate();
        return matches[0];
    }
}
public static class TrialSubscriptionPolicy
{
    public const int FreeTaxpayerLimit = 1;
    public const int FreeConfirmedInvoiceLimit = 2;

    /// <summary>Checks before a new taxpayer is created; this does not reserve a slot.</summary>
    public static bool CanCreateTaxpayer(bool hasEligiblePaidSubscription,
        int customerTaxpayers)
    {
        if(customerTaxpayers<0)throw new ArgumentOutOfRangeException(nameof(customerTaxpayers));
        return hasEligiblePaidSubscription || customerTaxpayers<FreeTaxpayerLimit;
    }
    /// <summary>Use with serialized server-side reservations, not a client-side counter.
    /// A confirmed invoice consumes the reserved entitlement once.</summary>
    public static bool CanReserveInvoice(bool hasEligiblePaidSubscription,
        int confirmedTrialInvoices, int reservedTrialInvoices)
    {
        if(confirmedTrialInvoices<0 || reservedTrialInvoices<0)
            throw new ArgumentOutOfRangeException("Invalid trial counters.");
        return hasEligiblePaidSubscription ||
            (long)confirmedTrialInvoices+reservedTrialInvoices<FreeConfirmedInvoiceLimit;
    }
}
