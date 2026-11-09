using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record InvoiceLineageNode(Guid InvoiceId,string Subject,
    string? FiscalTaxId,DateTime IssuedAtUtc,string DispatchState,
    string? AuthorityReference,string? AuthorityStatus,DateTime? SentAtUtc,
    bool CanResumeSubmission = false,string? SystemCode = null,
    string? ReferenceFiscalTaxId = null);
public sealed record InvoiceLineageView(bool HasDispatchAttempt,bool HasChanges,
    bool OutcomeUnknown,IReadOnlyList<InvoiceLineageNode> Nodes);

/// <summary>
/// Reads dispatched documents and durable legal or rejected-send replacement
/// relations without inventing predecessor links.
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
        invoiceId=await ResolveLineageRootAsync(db,owner.Value,taxpayerId,invoiceId,ct);
        var invoice=await db.InvoiceDocuments.AsNoTracking()
            .Where(i=>i.Id==invoiceId&&i.TaxpayerId==taxpayerId&&i.CustomerUserId==owner.Value)
            .Select(i=>new{i.Id,i.Subject,i.IssueDateUtc})
            .SingleOrDefaultAsync(ct);
        if(invoice is null)return (InvoiceApiDecision.NotFound,null);
        var effectiveCodes=await RejectedInvoiceReplacementQuery.ReadEffectiveSystemCodesAsync(
            db,[invoiceId],[owner.Value],[taxpayerId],ct);
        var rootSystemCode=effectiveCodes.GetValueOrDefault(invoiceId);
        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=System.Data.ConnectionState.Open;
        if(opened)await conn.OpenAsync(ct);
        try
        {
            await using var predecessor=conn.CreateCommand();
            predecessor.CommandText="""
SELECT predecessor_fiscal_tax_id FROM (
 SELECT predecessor_fiscal_tax_id,created_at_utc FROM sandbox_invoice_legal_relations
 WHERE successor_invoice_id=@invoice AND customer_user_id=@owner AND taxpayer_id=@taxpayer
 UNION ALL
 SELECT predecessor_fiscal_tax_id,created_at_utc FROM authority_invoice_legal_relations
 WHERE successor_invoice_id=@invoice AND customer_user_id=@owner AND taxpayer_id=@taxpayer
) refs ORDER BY created_at_utc DESC LIMIT 1
""";
            foreach(var (name,value) in new (string,object)[]{("invoice",invoiceId),
                ("taxpayer",taxpayerId),("owner",owner.Value)})
            {var p=predecessor.CreateParameter();p.ParameterName=name;p.Value=value;predecessor.Parameters.Add(p);}
            var referenceFiscalTaxId=await predecessor.ExecuteScalarAsync(ct) as string;
            await using var cmd=conn.CreateCommand();
            cmd.CommandText="""
SELECT d.state,d.created_at_utc,
 (SELECT r.authority_reference FROM invoice_dispatch_receipts r
  WHERE r.operation_id=d.id AND r.receipt_kind=1
  ORDER BY r.observed_at_utc DESC LIMIT 1),
 COALESCE((SELECT ir.report_json->>'Status' FROM production_authority_inquiry_reports ir
  WHERE ir.operation_id=d.id AND ir.customer_user_id=d.customer_user_id
    AND ir.taxpayer_id=d.taxpayer_id AND ir.invoice_id=d.invoice_id
  ORDER BY ir.observed_at_utc DESC,ir.id DESC LIMIT 1),
 (SELECT ir.report_json->>'Status' FROM sandbox_authority_inquiry_reports ir
  WHERE ir.operation_id=d.id AND ir.customer_user_id=d.customer_user_id
    AND ir.taxpayer_id=d.taxpayer_id AND ir.invoice_id=d.invoice_id
  ORDER BY ir.observed_at_utc DESC,ir.id DESC LIMIT 1)),
 (SELECT f.fiscal_tax_id FROM (
   SELECT fiscal_tax_id,invoice_id,taxpayer_id,customer_user_id,created_at_utc
   FROM sandbox_invoice_fiscal_reservations
   UNION ALL
   SELECT fiscal_tax_id,invoice_id,taxpayer_id,customer_user_id,created_at_utc
   FROM authority_invoice_fiscal_reservations
  ) f
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
            // Dispatch operation creation is not proof of the external send time.
            var reference=reader.IsDBNull(2)?null:reader.GetString(2);
            var official=reader.IsDBNull(3)?null:reader.GetString(3);
            var taxId=reader.IsDBNull(4)?null:reader.GetString(4);
            var label=state switch {
                1=>"Prepared",2=>"Signed",3=>"Reserved",4=>"Sending",5=>"Acknowledged",
                6=>"Indeterminate",7=>"RetryEligible",8=>"FailedTerminal",_=>"Unknown"
            };
            // A prepared/armed operation is not evidence of an actual submission attempt.
            var attempted=state is 4 or 5 or 6 or 8;
            var node=new InvoiceLineageNode(invoiceId,invoice.Subject.ToString(),taxId,
                invoice.IssueDateUtc,label,reference,official,null,SystemCode:rootSystemCode,
                ReferenceFiscalTaxId:referenceFiscalTaxId);
            await reader.DisposeAsync();
            var nodes=new List<InvoiceLineageNode>{node};
            await using var related=conn.CreateCommand();
            related.CommandText="""
WITH RECURSIVE lineage AS (
 SELECT r.id,r.predecessor_invoice_id,r.successor_invoice_id,r.predecessor_fiscal_tax_id,
  r.customer_user_id,r.taxpayer_id,
  r.created_at_utc,r.subject,r.state,r.is_production,1 AS depth,
  ARRAY[r.predecessor_invoice_id,r.successor_invoice_id] AS visited
 FROM (
  SELECT id,predecessor_invoice_id,successor_invoice_id,predecessor_fiscal_tax_id,customer_user_id,taxpayer_id,
   created_at_utc,subject,state,false AS is_production
  FROM sandbox_invoice_legal_relations
  UNION ALL
  SELECT id,predecessor_invoice_id,successor_invoice_id,predecessor_fiscal_tax_id,customer_user_id,taxpayer_id,
   created_at_utc,subject,state,true AS is_production
  FROM authority_invoice_legal_relations
  UNION ALL
  SELECT id,predecessor_invoice_id,successor_invoice_id,NULL::varchar AS predecessor_fiscal_tax_id,
   customer_user_id,taxpayer_id,created_at_utc,1 AS subject,1 AS state,true AS is_production
  FROM invoice_rejection_replacements
 ) r
 WHERE r.predecessor_invoice_id=@invoice
  AND r.customer_user_id=@owner AND r.taxpayer_id=@taxpayer
  AND r.state IN (1,2,4)
 UNION ALL
 SELECT r.id,r.predecessor_invoice_id,r.successor_invoice_id,r.predecessor_fiscal_tax_id,
  r.customer_user_id,r.taxpayer_id,
  r.created_at_utc,r.subject,r.state,r.is_production,lineage.depth+1,
  lineage.visited||r.successor_invoice_id
 FROM lineage
 JOIN (
  SELECT id,predecessor_invoice_id,successor_invoice_id,predecessor_fiscal_tax_id,customer_user_id,taxpayer_id,
   created_at_utc,subject,state,false AS is_production
  FROM sandbox_invoice_legal_relations
  UNION ALL
  SELECT id,predecessor_invoice_id,successor_invoice_id,predecessor_fiscal_tax_id,customer_user_id,taxpayer_id,
   created_at_utc,subject,state,true AS is_production
  FROM authority_invoice_legal_relations
  UNION ALL
  SELECT id,predecessor_invoice_id,successor_invoice_id,NULL::varchar AS predecessor_fiscal_tax_id,
   customer_user_id,taxpayer_id,created_at_utc,1 AS subject,1 AS state,true AS is_production
  FROM invoice_rejection_replacements
 ) r ON r.predecessor_invoice_id=lineage.successor_invoice_id
  AND r.customer_user_id=lineage.customer_user_id AND r.taxpayer_id=lineage.taxpayer_id
 WHERE lineage.depth<100 AND NOT r.successor_invoice_id=ANY(lineage.visited)
  AND r.state IN (1,2,4)
)
SELECT d.id,d.subject,d.issue_date_utc,
  (SELECT f.fiscal_tax_id FROM (
   SELECT fiscal_tax_id,invoice_id,taxpayer_id,customer_user_id,created_at_utc
   FROM sandbox_invoice_fiscal_reservations
   UNION ALL
   SELECT fiscal_tax_id,invoice_id,taxpayer_id,customer_user_id,created_at_utc
   FROM authority_invoice_fiscal_reservations
  ) f
  WHERE f.invoice_id=d.id AND f.customer_user_id=d.customer_user_id
    AND f.taxpayer_id=d.taxpayer_id ORDER BY f.created_at_utc DESC LIMIT 1),
 COALESCE((SELECT ir.report_json->>'Status' FROM production_authority_inquiry_reports ir
  WHERE ir.invoice_id=d.id AND ir.customer_user_id=d.customer_user_id
    AND ir.taxpayer_id=d.taxpayer_id
  ORDER BY ir.observed_at_utc DESC,ir.id DESC LIMIT 1),
 (SELECT ir.report_json->>'Status' FROM sandbox_authority_inquiry_reports ir
  WHERE ir.invoice_id=d.id AND ir.customer_user_id=d.customer_user_id
    AND ir.taxpayer_id=d.taxpayer_id
  ORDER BY ir.observed_at_utc DESC,ir.id DESC LIMIT 1)),
  (SELECT rc.authority_reference FROM invoice_dispatch_receipts rc
  JOIN invoice_dispatch_operations op ON op.id=rc.operation_id
  WHERE op.invoice_id=d.id AND op.customer_user_id=d.customer_user_id
    AND op.taxpayer_id=d.taxpayer_id AND rc.receipt_kind=1
  ORDER BY rc.observed_at_utc DESC LIMIT 1),
 (SELECT op.state FROM invoice_dispatch_operations op
  WHERE op.invoice_id=d.id AND op.customer_user_id=d.customer_user_id
    AND op.taxpayer_id=d.taxpayer_id
  ORDER BY op.created_at_utc DESC LIMIT 1),
  lineage.is_production,lineage.state,lineage.depth,lineage.created_at_utc,lineage.id,
  (SELECT CASE WHEN EXISTS(SELECT 1 FROM invoice_rejection_replacements rr
      WHERE rr.id=lineage.id AND rr.successor_invoice_id=d.id)
     THEN @root_system_code ELSE sr.system_code END
   FROM invoice_support_references sr
   WHERE sr.source_id=d.id AND sr.source_kind='document' LIMIT 1),
  lineage.predecessor_fiscal_tax_id
FROM lineage
JOIN invoice_documents d ON d.id=lineage.successor_invoice_id
 AND d.taxpayer_id=lineage.taxpayer_id AND d.customer_user_id=lineage.customer_user_id
WHERE lineage.customer_user_id=@owner AND lineage.taxpayer_id=@taxpayer
ORDER BY lineage.depth,lineage.created_at_utc,lineage.id
""";
            foreach(var (name,value) in new (string,object)[]{
                ("invoice",invoiceId),("taxpayer",taxpayerId),("owner",owner.Value),
                ("root_system_code",rootSystemCode is null?DBNull.Value:rootSystemCode)})
            {var p=related.CreateParameter();p.ParameterName=name;p.Value=value;related.Parameters.Add(p);}
            await using var next=await related.ExecuteReaderAsync(ct);
            while(await next.ReadAsync(ct))
            {
                var dispatchState=next.IsDBNull(6)?0:next.GetInt32(6);
                var status=dispatchState switch{0=>"NotPrepared",1=>"Prepared",2=>"Signed",
                    3=>"Reserved",4=>"Sending",5=>"Acknowledged",6=>"Indeterminate",
                    7=>"RetryEligible",8=>"FailedTerminal",_=>"Unknown"};
                var subject=(ArginTaxPayer.Domain.Invoices.InvoiceSubject)next.GetInt32(1);
                var canResume=next.GetBoolean(7) && next.GetInt32(8)==1 &&
                    dispatchState==0 && next.IsDBNull(3) &&
                    subject==ArginTaxPayer.Domain.Invoices.InvoiceSubject.Correction;
                nodes.Add(new(next.GetGuid(0),
                    subject.ToString(),
                    next.IsDBNull(3)?null:next.GetString(3),next.GetDateTime(2),status,
                    next.IsDBNull(5)?null:next.GetString(5),
                    next.IsDBNull(4)?null:next.GetString(4),null,canResume,
                    next.IsDBNull(12)?null:next.GetString(12),
                    next.IsDBNull(13)?null:next.GetString(13)));
            }
            return (InvoiceApiDecision.Success,
                new(attempted,nodes.Count>1,state is 4 or 6,nodes));
        }
        finally{if(opened)await conn.CloseAsync();}
    }

    private static async Task<Guid> ResolveLineageRootAsync(ArginTaxPayerDbContext db,
        Guid owner,Guid taxpayer,Guid invoiceId,CancellationToken ct)
    {
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=System.Data.ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            await using var command=connection.CreateCommand();
            command.CommandText="""
WITH RECURSIVE ancestors AS (
 SELECT @invoice::uuid AS invoice_id,0 AS depth,ARRAY[@invoice::uuid] AS visited
 UNION ALL
 SELECT r.predecessor_invoice_id,a.depth+1,a.visited||r.predecessor_invoice_id
 FROM ancestors a JOIN (
  SELECT predecessor_invoice_id,successor_invoice_id,customer_user_id,taxpayer_id
  FROM sandbox_invoice_legal_relations
  UNION ALL
  SELECT predecessor_invoice_id,successor_invoice_id,customer_user_id,taxpayer_id
  FROM authority_invoice_legal_relations
  UNION ALL
  SELECT predecessor_invoice_id,successor_invoice_id,customer_user_id,taxpayer_id
  FROM invoice_rejection_replacements
 ) r ON r.successor_invoice_id=a.invoice_id
  AND r.customer_user_id=@owner AND r.taxpayer_id=@taxpayer
 WHERE a.depth<100 AND NOT r.predecessor_invoice_id=ANY(a.visited)
)
SELECT invoice_id FROM ancestors ORDER BY depth DESC LIMIT 1
""";
            foreach(var (name,value) in new (string,object)[]{("invoice",invoiceId),
                ("owner",owner),("taxpayer",taxpayer)})
            {var p=command.CreateParameter();p.ParameterName=name;p.Value=value;command.Parameters.Add(p);}
            return await command.ExecuteScalarAsync(ct) is Guid root?root:invoiceId;
        }
        finally{if(opened)await connection.CloseAsync();}
    }
}
