using System;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;
namespace ArginTaxPayer.Tests;
public sealed class Phase08Step18InvoiceBulkIdentityTests
{
    [Fact]
    public void Stable_key_is_same_on_retry_but_different_for_other_customer_or_key()
    {
        var owner=Guid.NewGuid();
        var stable=InvoiceBulkIdentity.StableWorkspaceId(owner,"invoice:INV-001");
        Assert.Equal(stable,InvoiceBulkIdentity.StableWorkspaceId(owner,"invoice:INV-001"));
        Assert.NotEqual(stable,InvoiceBulkIdentity.StableWorkspaceId(owner,"invoice:INV-002"));
        Assert.NotEqual(stable,InvoiceBulkIdentity.StableWorkspaceId(Guid.NewGuid(),"invoice:INV-001"));
    }

    [Fact]
    public void Fingerprint_rejects_changed_invoice_data_for_same_key()
    {
        var draft=new SaveInvoiceEntryRequest(null,Guid.NewGuid(),0,2,
            new DateTime(2026,10,20,9,0,0,DateTimeKind.Utc),Guid.NewGuid(),1,1,
            [new InvoiceEntryLine(null,"item",1m,1000m,0m,10m)],
            SettlementMethod:InvoiceDraftSettlementMethod.Cash,
            CashAmountRial:1100m,CreditAmountRial:0m,
            BulkImportKey:"invoice:INV-001");
        var hash=InvoiceBulkIdentity.Fingerprint(draft);
        Assert.Equal(hash,InvoiceBulkIdentity.Fingerprint(draft));
        var modified=draft with { CashAmountRial=2200m };
        Assert.NotEqual(hash,InvoiceBulkIdentity.Fingerprint(modified));
    }
}
