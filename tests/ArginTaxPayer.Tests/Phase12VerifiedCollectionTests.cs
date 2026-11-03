using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;
public sealed class Phase12VerifiedCollectionTests
{
    [Fact]
    public void Partial_collections_reduce_receivable_without_overpayment()
    {
        var pending=new ReceivableBalance(1_000_000,0);
        var partial=pending.Collect(400_000);
        Assert.Equal(600_000,partial.OutstandingIrr);
        Assert.Equal(CommercialOrderStatus.PartiallyPaid,partial.Status);
        var final=partial.Collect(600_000);
        Assert.Equal(CommercialOrderStatus.Paid,final.Status);
        Assert.Throws<InvalidOperationException>(()=>final.Collect(1));
    }
    [Fact]
    public void Gateway_confirmation_requires_all_provider_evidence()
    {
        CollectionVerificationPolicy.RequireGatewayVerification(true,true,true,true);
        Assert.Throws<InvalidOperationException>(()=>
            CollectionVerificationPolicy.RequireGatewayVerification(true,false,true,true));
        Assert.Throws<InvalidOperationException>(()=>
            CollectionVerificationPolicy.RequireGatewayVerification(true,true,false,true));
        Assert.Throws<InvalidOperationException>(()=>
            CollectionVerificationPolicy.RequireGatewayVerification(false,true,true,true));
    }
    [Fact]
    public void Collection_rejects_missing_reference_and_nonpositive_payment()
    {
        var c=new VerifiedCollection(Guid.NewGuid(),Guid.NewGuid(),
            CollectionChannel.Cash,250_000,"RECEIPT-001",DateTime.UtcNow);
        c.Validate();
        Assert.Throws<ArgumentException>(()=>(c with {AmountIrr=0}).Validate());
        Assert.Throws<ArgumentException>(()=>(c with {ExternalReference=" "}).Validate());
    }
}
