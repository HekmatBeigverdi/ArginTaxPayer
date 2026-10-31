namespace ArginTaxPayer.Domain.Subscriptions;

/// <summary>Provider routing is independent from invoice value and financial ownership.</summary>
public enum PaymentGatewayProvider { Sep = 1, Zarinpal = 2 }

public sealed record PaymentGatewayRoute(PaymentGatewayProvider Provider, bool Enabled, int Priority);

public static class PaymentGatewayRouting
{
    public static PaymentGatewayProvider Select(IReadOnlyCollection<PaymentGatewayRoute> routes)
    {
        var eligible = routes
            .Where(x => x.Enabled && Enum.IsDefined(x.Provider) && x.Priority > 0)
            .OrderBy(x => x.Priority)
            .ThenBy(x => (int)x.Provider)
            .ToArray();
        if (eligible.Length == 0)
            throw new InvalidOperationException("No payment gateway is enabled.");
        return eligible[0].Provider;
    }

    public static readonly IReadOnlyCollection<PaymentGatewayRoute> DefaultRoutes =
    [
        new(PaymentGatewayProvider.Sep, true, 1),
        new(PaymentGatewayProvider.Zarinpal, true, 2)
    ];
}

/// <summary>Independent server-side confirmation is mandatory; never trust callback parameters.</summary>
public sealed record GatewayVerificationEvidence(
    PaymentGatewayProvider Provider,
    Guid OrderId,
    string PaymentAttemptKey,
    string ProviderTransactionId,
    string MerchantId,
    long AmountIrr,
    bool ProviderVerified,
    DateTime VerifiedAtUtc)
{
    public void RequireMatch(Guid orderId, string attemptKey, string merchantId, long amountIrr)
    {
        if (!Enum.IsDefined(Provider) || !ProviderVerified ||
            OrderId != orderId || PaymentAttemptKey != attemptKey ||
            MerchantId != merchantId || string.IsNullOrWhiteSpace(ProviderTransactionId) ||
            AmountIrr != amountIrr || amountIrr <= 0 ||
            VerifiedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Provider verification mismatch.");
    }
}
