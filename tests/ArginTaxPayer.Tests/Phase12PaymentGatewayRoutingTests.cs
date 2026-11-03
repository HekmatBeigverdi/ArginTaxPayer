using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12PaymentGatewayRoutingTests
{
    [Fact]
    public void Sep_is_primary_and_zarinpal_is_fallback()
    {
        Assert.Equal(PaymentGatewayProvider.Sep,
            PaymentGatewayRouting.Select(PaymentGatewayRouting.DefaultRoutes));
        Assert.Equal(PaymentGatewayProvider.Zarinpal, PaymentGatewayRouting.Select(
            [new(PaymentGatewayProvider.Sep,false,1),
             new(PaymentGatewayProvider.Zarinpal,true,2)]));
    }

    [Fact]
    public void Disabled_gateways_never_route()
    {
        Assert.Throws<InvalidOperationException>(() => PaymentGatewayRouting.Select(
            [new(PaymentGatewayProvider.Sep,false,1),
             new(PaymentGatewayProvider.Zarinpal,false,2)]));
    }

    [Fact]
    public void Gateway_evidence_must_match_order_attempt_merchant_and_amount()
    {
        var order=Guid.NewGuid();
        var evidence=new GatewayVerificationEvidence(PaymentGatewayProvider.Sep,
            order,"attempt-001","reference-001","merchant-001",2_500_000,true,DateTime.UtcNow);
        evidence.RequireMatch(order,"attempt-001","merchant-001",2_500_000);
        Assert.Throws<InvalidOperationException>(() =>
            evidence.RequireMatch(order,"attempt-001","merchant-001",2_500_001));
        Assert.Throws<InvalidOperationException>(() =>
            evidence.RequireMatch(order,"attempt-002","merchant-001",2_500_000));
        Assert.Throws<InvalidOperationException>(() =>
            evidence.RequireMatch(order,"attempt-001","merchant-002",2_500_000));
        Assert.Throws<InvalidOperationException>(() =>
            (evidence with {ProviderVerified=false}).RequireMatch(
                order,"attempt-001","merchant-001",2_500_000));
    }
}
