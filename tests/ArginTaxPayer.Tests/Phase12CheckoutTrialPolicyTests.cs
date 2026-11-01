using ArginTaxPayer.Domain.Subscriptions;
using Xunit;
namespace ArginTaxPayer.Tests;
public sealed class Phase12CheckoutTrialPolicyTests
{
    [Fact] public void Trial_allows_one_taxpayer_and_two_reserved_or_confirmed_invoices()
    {
        Assert.True(TrialSubscriptionPolicy.CanCreateTaxpayer(false,0));
        Assert.False(TrialSubscriptionPolicy.CanCreateTaxpayer(false,1));
        Assert.True(TrialSubscriptionPolicy.CanReserveInvoice(false,0,0));
        Assert.True(TrialSubscriptionPolicy.CanReserveInvoice(false,1,0));
        Assert.False(TrialSubscriptionPolicy.CanReserveInvoice(false,1,1));
        Assert.False(TrialSubscriptionPolicy.CanReserveInvoice(false,2,0));
        Assert.True(TrialSubscriptionPolicy.CanReserveInvoice(true,2,1));
    }
    [Fact] public void Official_invoice_adds_configured_vat_and_freezes_base()
    {
        var invoice=new SubscriptionCheckoutQuote(1_000_000,1000,true);
        Assert.Equal(100_000,invoice.TaxAmountIrr);
        Assert.Equal(1_100_000,invoice.TotalIrr);
        var noInvoice=new SubscriptionCheckoutQuote(1_000_000,1000,false);
        Assert.Equal(1_000_000,noInvoice.TotalIrr);
        Assert.Equal(0,noInvoice.TaxAmountIrr);
    }
    [Fact] public void Gateway_selection_is_scoped_to_partner_and_invoice_mode()
    {
        var partner=Guid.NewGuid();
        var other=Guid.NewGuid();
        var gateways=new[]{
            new GatewayMerchantConfiguration(other,SubscriptionGateway.Sep,true,"other",true),
            new GatewayMerchantConfiguration(partner,SubscriptionGateway.Zarinpal,true,"p-vat-z",true),
            new GatewayMerchantConfiguration(partner,SubscriptionGateway.Sep,false,"p-no-vat",true)
        };
        Assert.Equal(SubscriptionGateway.Zarinpal,
            SubscriptionGatewaySelection.Select(gateways,partner,true).Provider);
        Assert.Equal(SubscriptionGateway.Sep,
            SubscriptionGatewaySelection.Select(gateways,partner,false).Provider);
        Assert.Throws<InvalidOperationException>(()=>
            SubscriptionGatewaySelection.Select(gateways,other,false));
    }
}
