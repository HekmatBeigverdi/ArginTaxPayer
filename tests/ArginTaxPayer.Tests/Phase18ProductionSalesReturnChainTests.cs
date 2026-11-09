using System.IO;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18ProductionSalesReturnChainTests
{
    [Theory]
    [InlineData(InvoiceSubject.Original)]
    [InlineData(InvoiceSubject.Correction)]
    [InlineData(InvoiceSubject.SalesReturn)]
    public void Finalized_nonterminal_nodes_can_be_sales_return_predecessors(InvoiceSubject subject)
    {
        Assert.True(Phase18ProductionSalesReturnService.CanBeSalesReturnPredecessor(
            subject, InvoiceLifecycleState.Finalized));
    }

    [Theory]
    [InlineData(InvoiceSubject.Cancellation, InvoiceLifecycleState.Finalized)]
    [InlineData(InvoiceSubject.Original, InvoiceLifecycleState.Draft)]
    [InlineData(InvoiceSubject.Correction, InvoiceLifecycleState.Draft)]
    public void Cancellation_and_unfinalized_nodes_cannot_be_sales_return_predecessors(
        InvoiceSubject subject, InvoiceLifecycleState state)
    {
        Assert.False(Phase18ProductionSalesReturnService.CanBeSalesReturnPredecessor(subject, state));
    }

    [Fact]
    public void Production_return_uses_selected_predecessor_snapshot_tax_id_and_relation()
    {
        var source = File.ReadAllText(FindService());
        Assert.Contains("invoice_id={predecessorId} LIMIT 1", source);
        Assert.Contains("snapshot[\"referenceTaxId\"] = referenceTaxId", source);
        Assert.Contains("snapshot[\"predecessorInvoiceId\"] = predecessorId.ToString(\"D\")", source);
        Assert.Contains("ReadSalesReturnBasisAsync(", source);
        Assert.Contains("returnBasis.Lines", source);
        Assert.Contains("write.PredecessorInvoiceId},{invoiceId}", source);
    }

    [Fact]
    public void Production_return_rechecks_active_chain_claim_inside_persistence_transaction()
    {
        var source = File.ReadAllText(FindService());
        Assert.Contains("AND predecessor_invoice_id={write.PredecessorInvoiceId}", source);
        Assert.Contains("AND subject IN (2,3) AND state IN (1,2,4)", source);
        Assert.Contains("AllowedLines(returnBasis.Lines,returnLines,returnBasis.AlreadyReturned)", source);
        Assert.Contains("active_related_invoice", source);
    }

    private static string FindService()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "ArginTaxPayer.Infrastructure",
                "Persistence", "Invoices", "Phase18ProductionSalesReturnService.cs");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException("Phase18ProductionSalesReturnService.cs");
    }
}
