using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record InvoiceLineageNode(Guid InvoiceId,string Subject,
    string? FiscalTaxId,DateTime IssuedAtUtc,string DispatchState,
    string? AuthorityReference,string? AuthorityStatus,DateTime? SentAtUtc);
public sealed record InvoiceLineageView(bool HasDispatchAttempt,bool HasChanges,
    bool OutcomeUnknown,IReadOnlyList<InvoiceLineageNode> Nodes);

/// <summary>
/// First Step25 slice: read the genuine dispatched document without inventing
/// predecessor links. Related document edges require durable legal relations.
/// </summary>
public sealed class Phase10InvoiceLineageReadService(
    ArginTaxPayerDbContext db,TaxpayerScopeService scope)
{
    public async Task<(InvoiceApiDecision Decision,InvoiceLineageView? Value)> ReadAsync(
        string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return (InvoiceApiDecision.Unauthenticated,null);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return (InvoiceApiDecision.Forbidden,null);
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if(owner is null)return (InvoiceApiDecision.NotFound,null);
        var invoice=await db.InvoiceDocuments.AsNoTracking()
            .Where(i=>i.Id==invoiceId&&i.TaxpayerId==taxpayerId&&i.CustomerUserId==owner.Value)
            .Select(i=>new{i.Id,i.Subject,i.IssueDateUtc})
            .SingleOrDefaultAsync(ct);
        if(invoice is null)return (InvoiceApiDecision.NotFound,null);
        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=System.Data.ConnectionState.Open;
        if(opened)await conn.OpenAsync(ct);
        try
        {
            await using var cmd=conn.CreateCommand();
            cmd.CommandText="""
SELECT d.state,d.created_at_utc,
 (SELECT r.authority_reference FROM invoice_dispatch_receipts r
  WHERE r.operation_id=d.id AND r.receipt_kind=1
  ORDER BY r.observed_at_utc DESC LIMIT 1),
 (SELECT ir.report_json->>'Status' FROM sandbox_authority_inquiry_reports ir
  WHERE ir.operation_id=d.id AND ir.customer_user_id=d.customer_user_id
    AND ir.taxpayer_id=d.taxpayer_id AND ir.invoice_id=d.invoice_id
  ORDER BY ir.observed_at_utc DESC,ir.id DESC LIMIT 1),
 (SELECT f.fiscal_tax_id FROM sandbox_invoice_fiscal_reservations f
  WHERE f.invoice_id=d.invoice_id AND f.taxpayer_id=d.taxpayer_id
    AND f.customer_user_id=d.customer_user_id
  ORDER BY f.created_at_utc DESC LIMIT 1)
FROM invoice_dispatch_operations d
WHERE d.invoice_id=@invoice AND d.taxpayer_id=@taxpayer
 AND d.customer_user_id=@owner
ORDER BY d.invoice_revision DESC,d.created_at_utc DESC LIMIT 1
""";
            foreach(var (name,value) in new (string,object)[]{
                ("invoice",invoiceId),("taxpayer",taxpayerId),("owner",owner.Value)})
            {
                var p=cmd.CreateParameter();p.ParameterName=name;
                p.Value=value;cmd.Parameters.Add(p);
            }
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct))
                return (InvoiceApiDecision.Success,new(false,false,false,Array.Empty<InvoiceLineageNode>()));
            var state=reader.GetInt32(0);
            var created=reader.GetDateTime(1);
            var reference=reader.IsDBNull(2)?null:reader.GetString(2);
            var official=reader.IsDBNull(3)?null:reader.GetString(3);
            var taxId=reader.IsDBNull(4)?null:reader.GetString(4);
            var label=state switch {
                1=>"Prepared",3=>"Reserved",4=>"Sending",5=>"Acknowledged",
                6=>"Indeterminate",_=>"Unknown"
            };
            // A prepared/armed operation is not evidence of an actual submission attempt.
            var attempted=state is 4 or 5 or 6;
            var node=new InvoiceLineageNode(invoiceId,invoice.Subject.ToString(),taxId,
                invoice.IssueDateUtc,label,reference,official,attempted?created:null);
            return (InvoiceApiDecision.Success,
                new(attempted,false,state is 4 or 6,new[]{node}));
        }
        finally{if(opened)await conn.CloseAsync();}
    }
}
