using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08Step13SettlementPolicyTests
{
    [Fact]
    public void Incomplete_draft_may_have_no_terms_but_cannot_have_amounts_without_method()
    {
        Assert.True(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.Unknown, null, null, 100m));
        Assert.False(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.Unknown, 0m, 0m, 100m));
    }

    [Theory]
    [InlineData(InvoiceDraftSettlementMethod.Cash, 100, 0, true)]
    [InlineData(InvoiceDraftSettlementMethod.Cash, 99, 1, false)]
    [InlineData(InvoiceDraftSettlementMethod.Credit, 0, 100, true)]
    [InlineData(InvoiceDraftSettlementMethod.Credit, 1, 99, false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, 30, 70, true)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, 0, 100, false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, 100, 0, false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, 30, 69, false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, -1, 101, false)]
    public void Payment_terms_must_match_canonical_total_and_method(
        InvoiceDraftSettlementMethod method, int cash, int credit, bool expected)
    {
        Assert.Equal(expected, InvoiceDraftSettlementPolicy.IsConsistent(
            method, cash, credit, 100m));
    }
}
