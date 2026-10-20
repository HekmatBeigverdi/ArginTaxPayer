using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08Step12FinancialSummaryTests
{
    [Fact]
    public void Calculates_discount_vat_and_total_from_decimal_lines()
    {
        var lines = new[] { new InvoiceEntryLine(null, "کالا", 2m, 50.25m, 5.25m, 10m) };
        var result = InvoiceDraftSettlementPolicy.Calculate(lines);
        Assert.Equal(100.5m, result.GrossRial);
        Assert.Equal(10.5m, result.DiscountRial);
        Assert.Equal(90m, result.AfterDiscountRial);
        Assert.Equal(9m, result.PreviewVatRial);
        Assert.Equal(99m, result.PreviewGrandTotalRial);
    }

    [Fact]
    public void Rounds_each_line_before_aggregation()
    {
        var lines = new[] {
            new InvoiceEntryLine(null, "اول", 1m, 0.005m, 0m, 0m),
            new InvoiceEntryLine(null, "دوم", 1m, 0.005m, 0m, 0m)
        };
        var result = InvoiceDraftSettlementPolicy.Calculate(lines);
        Assert.Equal(0.02m, result.GrossRial);
        Assert.Equal(0.02m, result.PreviewGrandTotalRial);
    }

    [Fact]
    public void Settlement_consistency_uses_canonical_total()
    {
        var result = InvoiceDraftSettlementPolicy.Calculate(
            [new InvoiceEntryLine(null, "خدمت", 1m, 9100m, 0m, 10m)]);
        Assert.Equal(10010m, result.PreviewGrandTotalRial);
        Assert.True(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.CashAndCredit, 5000m, 5010m,
            result.PreviewGrandTotalRial));
        Assert.False(InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.CashAndCredit, 5000m, 5009m,
            result.PreviewGrandTotalRial));
    }
}
