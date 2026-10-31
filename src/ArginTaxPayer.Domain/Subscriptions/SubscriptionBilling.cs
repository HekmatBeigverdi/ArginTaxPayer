namespace ArginTaxPayer.Domain.Subscriptions;

public enum CommercialOrderStatus { Pending = 1, PartiallyPaid = 2, Paid = 3, Voided = 4 }

/// <summary>Internal commercial invoice; not an Iranian tax-authority e-invoice.</summary>
public sealed record CommercialOrderSnapshot(Guid OrderId, Guid CustomerUserId,
    Guid PlanId, Guid PriceVersionId, Guid RootAdminUserId, Guid? PartnerUserId,
    long GrossAmountIrr, DateTime IssuedAtUtc)
{
    public void Validate()
    {
        if (OrderId == Guid.Empty || CustomerUserId == Guid.Empty || PlanId == Guid.Empty ||
            PriceVersionId == Guid.Empty || RootAdminUserId == Guid.Empty ||
            GrossAmountIrr < 0 || IssuedAtUtc.Kind != DateTimeKind.Utc ||
            PartnerUserId == Guid.Empty)
            throw new ArgumentException("Invalid commercial order snapshot.");
    }
}

public sealed record ReceivableBalance(long GrossIrr, long CollectedIrr)
{
    public long OutstandingIrr => checked(GrossIrr - CollectedIrr);
    public CommercialOrderStatus Status => CollectedIrr == GrossIrr
        ? CommercialOrderStatus.Paid
        : CollectedIrr == 0 ? CommercialOrderStatus.Pending : CommercialOrderStatus.PartiallyPaid;

    public void Validate()
    {
        if (GrossIrr < 0 || CollectedIrr < 0 || CollectedIrr > GrossIrr)
            throw new InvalidOperationException("Receivable balance is invalid.");
    }

    public ReceivableBalance Collect(long amountIrr)
    {
        Validate();
        if (amountIrr <= 0 || amountIrr > OutstandingIrr)
            throw new InvalidOperationException("Invalid collection amount.");
        var result = this with { CollectedIrr = checked(CollectedIrr + amountIrr) };
        result.Validate();
        return result;
    }
}
