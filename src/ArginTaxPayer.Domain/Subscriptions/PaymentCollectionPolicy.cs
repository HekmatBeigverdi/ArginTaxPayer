namespace ArginTaxPayer.Domain.Subscriptions;

public enum CollectionChannel { Cash = 1, BankTransfer = 2, Gateway = 3 }

public sealed record VerifiedCollection(
    Guid OrderId, Guid CustomerUserId, CollectionChannel Channel,
    long AmountIrr, string ExternalReference, DateTime VerifiedAtUtc)
{
    public void Validate()
    {
        if (OrderId == Guid.Empty || CustomerUserId == Guid.Empty ||
            !Enum.IsDefined(Channel) || AmountIrr <= 0 ||
            string.IsNullOrWhiteSpace(ExternalReference) || ExternalReference.Length > 160 ||
            VerifiedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid verified collection.");
    }
}

public static class CollectionVerificationPolicy
{
    // No browser redirect or submitted gateway token is sufficient on its own.
    // A trusted gateway verifier must check status, merchant, reference, amount and currency.
    public static void RequireGatewayVerification(bool gatewayConfirmed, bool merchantMatches,
        bool amountMatches, bool currencyIsIrr)
    {
        if (!gatewayConfirmed || !merchantMatches || !amountMatches || !currencyIsIrr)
            throw new InvalidOperationException("Gateway verification is incomplete.");
    }
}
