using ArginTaxPayer.Domain.Subscriptions;

namespace ArginTaxPayer.Application.Modules.Subscription;

/// <summary>Provider-specific adapters must implement both initiation and independent verification.
/// Never convert an unverified browser callback into payment credit.</summary>
public interface ISubscriptionPaymentGateway
{
    PaymentGatewayProvider Provider { get; }
    Task<GatewayPaymentInitiation> InitiateAsync(GatewayPaymentRequest request, CancellationToken ct);
    Task<GatewayVerificationEvidence> VerifyAsync(GatewayPaymentAttempt attempt, CancellationToken ct);
}

public sealed record GatewayPaymentRequest(Guid OrderId, string AttemptKey, long AmountIrr,
    Uri CallbackUri, string MerchantId);
public sealed record GatewayPaymentInitiation(Uri RedirectUri, string ProviderToken);
public sealed record GatewayPaymentAttempt(Guid OrderId, string AttemptKey, long AmountIrr,
    string MerchantId, string ProviderToken);

public sealed class PaymentGatewayNotConfiguredException(string provider)
    : InvalidOperationException($"Payment gateway {provider} is not configured.");
