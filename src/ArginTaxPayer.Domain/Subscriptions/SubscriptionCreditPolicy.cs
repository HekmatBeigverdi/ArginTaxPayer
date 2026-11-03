namespace ArginTaxPayer.Domain.Subscriptions;

public enum SubscriptionCreditAction { Reserve, Consume, Release }

public sealed record SubscriptionCreditState(long Granted, long Available, long Reserved, long Consumed)
{
    public void Validate()
    {
        if (Granted < 0 || Available < 0 || Reserved < 0 || Consumed < 0 ||
            Available > Granted || Reserved > Granted || Consumed > Granted ||
            Available + Reserved + Consumed != Granted)
            throw new InvalidOperationException("Credit conservation invariant violated.");
    }

    public SubscriptionCreditState Transition(SubscriptionCreditAction action, long units,
        long reservationRemaining)
    {
        Validate();
        if (units <= 0) throw new ArgumentOutOfRangeException(nameof(units));
        if (reservationRemaining < 0) throw new ArgumentOutOfRangeException(nameof(reservationRemaining));
        SubscriptionCreditState result = action switch
        {
            SubscriptionCreditAction.Reserve when Available >= units =>
                this with { Available = Available - units, Reserved = Reserved + units },
            SubscriptionCreditAction.Consume when reservationRemaining >= units && Reserved >= units =>
                this with { Reserved = Reserved - units, Consumed = Consumed + units },
            SubscriptionCreditAction.Release when reservationRemaining >= units && Reserved >= units =>
                this with { Reserved = Reserved - units, Available = Available + units },
            _ => throw new InvalidOperationException("Insufficient available or reserved credit.")
        };
        result.Validate();
        return result;
    }
}
