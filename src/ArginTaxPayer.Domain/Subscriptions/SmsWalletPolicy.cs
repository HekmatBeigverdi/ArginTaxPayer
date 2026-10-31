namespace ArginTaxPayer.Domain.Subscriptions;

/// <summary>SMS credit is separate from invoice quota and commercial order cash.</summary>
public sealed record SmsWalletBalance(long Granted, long Spent)
{
    public long Available => checked(Granted - Spent);
    public void Validate()
    {
        if(Granted<0 || Spent<0 || Spent>Granted)
            throw new InvalidOperationException("Invalid SMS wallet balance.");
    }
    public SmsWalletBalance Spend(long units)
    {
        Validate();
        if(units<=0 || units>Available)
            throw new InvalidOperationException("Insufficient SMS units.");
        return this with {Spent=checked(Spent+units)};
    }
    public SmsWalletBalance Grant(long units)
    {
        Validate();
        if(units<=0)throw new ArgumentOutOfRangeException(nameof(units));
        return this with {Granted=checked(Granted+units)};
    }
}
