using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase10CancellationEligibility(InvoiceApiDecision Decision,
    string Code,string? FiscalTaxId=null,bool Eligible=false);

/// <summary>
/// A conservative, read-only cancellation prerequisite check. Eligibility does NOT
/// authorize a send; only the transactional cancellation command may do that.
/// </summary>
public sealed class Phase10CancellationEligibilityService(
    ArginTaxPayerDbContext db,TaxpayerScopeService scope,
    Phase10LegacyReferenceInquiryService inquiry)
{
    public async Task<Phase10CancellationEligibility> ReadAsync(
        string? bearer,Guid taxpayerId,Guid invoiceId,CancellationToken ct)
    {
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(actor.Decision==TaxpayerScopeDecision.Unauthenticated?
                InvoiceApiDecision.Unauthenticated:InvoiceApiDecision.Forbidden,"access_denied");
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if(owner is null)return new(InvoiceApiDecision.NotFound,"not_found");
        var source=await db.InvoiceDocuments.AsNoTracking()
            .Where(i=>i.Id==invoiceId&&i.CustomerUserId==owner.Value&&i.TaxpayerId==taxpayerId)
            .Select(i=>new{i.Subject,i.State}).SingleOrDefaultAsync(ct);
        if(source is null)return new(InvoiceApiDecision.NotFound,"not_found");
        if(source.Subject==ArginTaxPayer.Domain.Invoices.InvoiceSubject.Cancellation)
            return new(InvoiceApiDecision.Conflict,"cannot_cancel_cancellation");
        var observed=await inquiry.ReadAsync(bearer,taxpayerId,invoiceId,ct);
        if(observed.Decision!=InvoiceApiDecision.Success||observed.AuthorityResult?.Status!="SUCCESS")
            return new(InvoiceApiDecision.Conflict,"accepted_predecessor_required");
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=System.Data.ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            await using var command=connection.CreateCommand();
            command.CommandText="""
SELECT f.fiscal_tax_id,
 EXISTS(SELECT 1 FROM sandbox_invoice_legal_relations r
  WHERE r.predecessor_invoice_id=@invoice AND r.taxpayer_id=@taxpayer
   AND r.customer_user_id=@owner AND r.subject=3 AND r.state IN (1,2,4))
FROM sandbox_invoice_fiscal_reservations f
WHERE f.invoice_id=@invoice AND f.taxpayer_id=@taxpayer
 AND f.customer_user_id=@owner ORDER BY f.created_at_utc DESC LIMIT 1
""";
            foreach(var (name,value) in new (string,object)[]{
                ("invoice",invoiceId),("taxpayer",taxpayerId),("owner",owner.Value)})
            {
                var p=command.CreateParameter();p.ParameterName=name;p.Value=value;
                command.Parameters.Add(p);
            }
            await using var reader=await command.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct))
                return new(InvoiceApiDecision.Conflict,"predecessor_taxid_not_reserved");
            var fiscalId=reader.GetString(0);
            if(reader.GetBoolean(1))
                return new(InvoiceApiDecision.Conflict,"cancellation_already_requested",fiscalId);
            return new(InvoiceApiDecision.Success,"eligible_for_cancellation_preflight",fiscalId,true);
        }
        finally{if(opened)await connection.CloseAsync();}
    }
}
