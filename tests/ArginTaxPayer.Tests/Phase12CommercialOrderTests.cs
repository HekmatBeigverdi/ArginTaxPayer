using ArginTaxPayer.Domain.Subscriptions;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12CommercialOrderTests
{
    [Fact]
    public void Partial_and_full_payments_keep_receivable_consistent()
    {
        var original = new ReceivableBalance(1_000_000, 0);
        original.Validate();
        Assert.Equal(CommercialOrderStatus.Pending, original.Status);
        var partial = original.Collect(200_000);
        Assert.Equal(800_000, partial.OutstandingIrr);
        Assert.Equal(CommercialOrderStatus.PartiallyPaid, partial.Status);
        var paid = partial.Collect(800_000);
        Assert.Equal(0, paid.OutstandingIrr);
        Assert.Equal(CommercialOrderStatus.Paid, paid.Status);
        Assert.Throws<InvalidOperationException>(() => paid.Collect(1));
    }

    [Fact]
    public void Negative_or_overcollected_balances_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new ReceivableBalance(100, 101).Validate());
        Assert.Throws<InvalidOperationException>(() => new ReceivableBalance(-1, 0).Validate());
        Assert.Throws<InvalidOperationException>(() => new ReceivableBalance(100, 0).Collect(101));
    }

    [Fact]
    public void Invoice_snapshot_requires_price_and_customer()
    {
        var order = new CommercialOrderSnapshot(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),null,500_000,DateTime.UtcNow);
        order.Validate();
        Assert.Throws<ArgumentException>(() => (order with { GrossAmountIrr = -1 }).Validate());
        Assert.Throws<ArgumentException>(() => (order with { CustomerUserId = Guid.Empty }).Validate());
    }
}
