using System;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Pure-domain Phase08 regression: monetary invariants and import identity.
/// No database writes, server credentials or external services required.</summary>
public sealed class Phase08Step19RegressionTests
{
    [Fact]
    public void Invoice_lines_use_decimal_truncation_and_reconcile()
    {
        var items = new[] {
            new InvoiceEntryLine(null, "A", 2m, 9100m, 100m, 10m),
            new InvoiceEntryLine(null, "B", 1m, 3000m, 0m, 10m)
        };
        var value = InvoiceDraftSettlementPolicy.Calculate(items);
        Assert.Equal(21200m, value.GrossRial);
        Assert.Equal(200m, value.DiscountRial);
        Assert.Equal(21000m, value.AfterDiscountRial);
        Assert.Equal(2100m, value.PreviewVatRial);
        Assert.Equal(23100m, value.PreviewGrandTotalRial);
        Assert.True(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.CashAndCredit, 11000m, 12100m,
            value.PreviewGrandTotalRial));
        Assert.False(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.CashAndCredit, 11000m, 12000m,
            value.PreviewGrandTotalRial));
    }

    [Fact]
    public void Incorrect_settlement_modes_do_not_pass()
    {
        Assert.False(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.Cash, 0m, 100m, 100m));
        Assert.False(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.Credit, 100m, 0m, 100m));
        Assert.False(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.CashAndCredit, 100m, 0m, 100m));
        Assert.False(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.Cash, -1m, 101m, 100m));
    }

    [Fact]
    public void Stable_import_identifier_is_scoped_to_customer_and_source_key()
    {
        var customer = Guid.Parse("e5f0030c-1477-4c25-91be-6cad816c42c8");
        var key = "invoice:INVOICE-001";
        Assert.Equal(InvoiceBulkIdentity.StableWorkspaceId(customer,key),
                     InvoiceBulkIdentity.StableWorkspaceId(customer,key));
        Assert.NotEqual(InvoiceBulkIdentity.StableWorkspaceId(customer,key),
                     InvoiceBulkIdentity.StableWorkspaceId(customer,"invoice:INVOICE-002"));
        Assert.NotEqual(InvoiceBulkIdentity.StableWorkspaceId(customer,key),
                     InvoiceBulkIdentity.StableWorkspaceId(Guid.NewGuid(),key));
    }
}
