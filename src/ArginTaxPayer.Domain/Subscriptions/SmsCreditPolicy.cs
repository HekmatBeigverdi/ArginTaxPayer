namespace ArginTaxPayer.Domain.Subscriptions;

public sealed record SmsCreditBalance(long GrantedIrr, long SpentIrr, long ReservedIrr)
{
    public long AvailableIrr
    {
        get
        {
            Validate();
            return checked(GrantedIrr - SpentIrr - ReservedIrr);
        }
    }

    public void Validate()
    {
        if (GrantedIrr < 0 || SpentIrr < 0 || ReservedIrr < 0 ||
            SpentIrr > GrantedIrr || ReservedIrr > GrantedIrr - SpentIrr)
            throw new InvalidOperationException("Invalid SMS credit balance.");
    }

    public SmsCreditBalance Grant(long amountIrr)
    {
        Validate();
        if (amountIrr <= 0) throw new ArgumentOutOfRangeException(nameof(amountIrr));
        return this with { GrantedIrr = checked(GrantedIrr + amountIrr) };
    }

    public SmsCreditBalance Reserve(long amountIrr)
    {
        Validate();
        if (amountIrr <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountIrr));
        if (amountIrr > AvailableIrr)
            throw new SmsInsufficientCreditException();
        return this with { ReservedIrr = checked(ReservedIrr + amountIrr) };
    }

    public SmsCreditBalance Settle(long reservedIrr, long chargeIrr)
    {
        Validate();
        if (reservedIrr <= 0 || reservedIrr > ReservedIrr || chargeIrr < 0)
            throw new ArgumentOutOfRangeException(nameof(reservedIrr));
        if (chargeIrr > reservedIrr)
            throw new SmsReservationExceededException();
        return this with
        {
            SpentIrr = checked(SpentIrr + chargeIrr),
            ReservedIrr = checked(ReservedIrr - reservedIrr)
        };
    }

    public SmsCreditBalance Release(long reservedIrr)
    {
        Validate();
        if (reservedIrr <= 0 || reservedIrr > ReservedIrr)
            throw new ArgumentOutOfRangeException(nameof(reservedIrr));
        return this with { ReservedIrr = ReservedIrr - reservedIrr };
    }
}

public sealed record SmsCustomerCharge(long ProviderCostIrr, int ServiceFeeBasisPoints,
    long ServiceFeeIrr, long TotalChargeIrr);

public static class SmsCreditPolicy
{
    public const int MaximumServiceFeeBasisPoints = 10_000;

    public static SmsCustomerCharge CalculateCharge(long providerCostIrr,
        int serviceFeeBasisPoints)
    {
        if (providerCostIrr < 0)
            throw new ArgumentOutOfRangeException(nameof(providerCostIrr));
        if (serviceFeeBasisPoints is < 0 or > MaximumServiceFeeBasisPoints)
            throw new ArgumentOutOfRangeException(nameof(serviceFeeBasisPoints));

        var fee = decimal.Ceiling((decimal)providerCostIrr * serviceFeeBasisPoints / 10_000m);
        var serviceFeeIrr = checked((long)fee);
        var total = checked(providerCostIrr + serviceFeeIrr);
        return new SmsCustomerCharge(providerCostIrr, serviceFeeBasisPoints,
            serviceFeeIrr, total);
    }
}

public sealed class SmsInsufficientCreditException()
    : InvalidOperationException("Insufficient SMS credit.");

public sealed class SmsReservationExceededException()
    : InvalidOperationException("SMS charge exceeded its reservation.");
