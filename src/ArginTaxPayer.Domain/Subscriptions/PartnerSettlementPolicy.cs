namespace ArginTaxPayer.Domain.Subscriptions;

public enum PartnerSettlementEventKind { Payment = 1, Reversal = 2 }

public sealed record PartnerSettlementPosition(long EarnedIrr, long PaidIrr, long ReversedIrr)
{
    public long NetSettledIrr => checked(PaidIrr - ReversedIrr);
    public long OutstandingIrr => checked(EarnedIrr - NetSettledIrr);
    public void Validate()
    {
        if (EarnedIrr < 0 || PaidIrr < 0 || ReversedIrr < 0 ||
            ReversedIrr > PaidIrr || NetSettledIrr > EarnedIrr)
            throw new InvalidOperationException("Invalid settlement position.");
    }
    public PartnerSettlementPosition Pay(long amountIrr)
    {
        Validate();
        if (amountIrr <= 0 || amountIrr > OutstandingIrr)
            throw new InvalidOperationException("Settlement exceeds payable balance.");
        var result = this with { PaidIrr = checked(PaidIrr + amountIrr) };
        result.Validate();
        return result;
    }
    public PartnerSettlementPosition Reverse(long amountIrr)
    {
        Validate();
        if (amountIrr <= 0 || amountIrr > NetSettledIrr)
            throw new InvalidOperationException("Reversal exceeds net settlement.");
        var result = this with { ReversedIrr = checked(ReversedIrr + amountIrr) };
        result.Validate();
        return result;
    }
}
