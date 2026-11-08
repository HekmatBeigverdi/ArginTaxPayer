using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Creates a separate Production cancellation successor and submits it once.</summary>
public sealed class Phase18ProductionCancellationService(
    ArginTaxPayerDbContext db,
    TaxpayerScopeService scope,
    Phase18InvoiceInquiryService inquiry,
    Phase10InvoiceDispatchReadService dispatch,
    Phase18InvoiceSubmissionService submission,
    InvoiceSubscriptionGate subscriptions,
    IConfiguration config)
{
    public async Task<Phase10CancellationEligibility> ReadAsync(string? bearer,Guid taxpayerId,Guid predecessorId,CancellationToken ct)
    {
        if(config["Phase18Production:LegalSuccessorsEnabled"]!="true"||config["Phase18Production:LegalSuccessorsOperatorApproved"]!="true")
            return new(InvoiceApiDecision.Conflict,"production_legal_successors_disabled");
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(actor.Decision==TaxpayerScopeDecision.Unauthenticated?InvoiceApiDecision.Unauthenticated:InvoiceApiDecision.Forbidden,"access_denied");
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1).Where(t=>t.Id==taxpayerId)
            .Select(t=>t.CustomerUserId).SingleOrDefaultAsync(ct);
        if(owner is null)return new(InvoiceApiDecision.NotFound,"not_found");
        var source=await db.InvoiceDocuments.AsNoTracking().Where(i=>i.Id==predecessorId&&i.TaxpayerId==taxpayerId&&i.CustomerUserId==owner.Value)
            .Select(i=>new{i.Subject,i.State}).SingleOrDefaultAsync(ct);
        if(source is null)return new(InvoiceApiDecision.NotFound,"not_found");
        if(source.Subject==InvoiceSubject.Cancellation)return new(InvoiceApiDecision.Conflict,"cannot_cancel_cancellation");
        if(source.State!=InvoiceLifecycleState.Finalized)return new(InvoiceApiDecision.Conflict,"predecessor_not_finalized");
        var sent=await dispatch.GetAsync(bearer,taxpayerId,predecessorId,ct);
        if(sent.Overview is not {Status:"Acknowledged",Environment:"Production",ReferenceNumber:not null} op)
            return new(InvoiceApiDecision.Conflict,"accepted_predecessor_required");
        var result=await inquiry.ReadAsync(bearer,taxpayerId,predecessorId,ct);
        if(result.Decision!=InvoiceApiDecision.Success||result.AuthorityResult?.Status!="SUCCESS"||result.ReferenceNumber!=op.ReferenceNumber)
            return new(InvoiceApiDecision.Conflict,"accepted_predecessor_required");
        var taxId=await db.Database.SqlQuery<string>($"""
SELECT fiscal_tax_id AS "Value" FROM authority_invoice_fiscal_reservations
WHERE customer_user_id={owner.Value} AND taxpayer_id={taxpayerId} AND invoice_id={predecessorId}
ORDER BY created_at_utc DESC LIMIT 1
""").FirstOrDefaultAsync(ct);
        if(taxId is not{Length:22})return new(InvoiceApiDecision.Conflict,"predecessor_taxid_not_reserved");
        var active=await db.Database.SqlQuery<int>($"""
SELECT 1 AS "Value" FROM authority_invoice_legal_relations
WHERE customer_user_id={owner.Value} AND taxpayer_id={taxpayerId} AND predecessor_invoice_id={predecessorId}
 AND subject=3 AND state IN (1,2,4)
 AND NOT EXISTS(SELECT 1 FROM production_authority_inquiry_reports r
   WHERE r.invoice_id=authority_invoice_legal_relations.successor_invoice_id
    AND r.customer_user_id=authority_invoice_legal_relations.customer_user_id
    AND r.taxpayer_id=authority_invoice_legal_relations.taxpayer_id
    AND r.report_json->>'Status'='FAILED'
    AND r.observed_at_utc=(SELECT max(r2.observed_at_utc) FROM production_authority_inquiry_reports r2
      WHERE r2.invoice_id=r.invoice_id AND r2.customer_user_id=r.customer_user_id AND r2.taxpayer_id=r.taxpayer_id))
LIMIT 1
""").AnyAsync(ct);
        return active?new(InvoiceApiDecision.Conflict,"cancellation_already_requested",taxId):
            new(InvoiceApiDecision.Success,"eligible_for_cancellation_preflight",taxId,true);
    }

    public async Task<CancellationSendResult> SendAsync(string? bearer,Guid taxpayerId,Guid predecessorId,CancellationToken ct)
    {
        if(config["Phase18Production:LegalSuccessorsEnabled"]!="true"||config["Phase18Production:LegalSuccessorsOperatorApproved"]!="true")
            return new(InvoiceApiDecision.Conflict,"production_legal_successors_disabled");
        var eligible=await ReadAsync(bearer,taxpayerId,predecessorId,ct);
        if(!eligible.Eligible||eligible.FiscalTaxId is not{Length:22} predecessorTaxId)
            return new(eligible.Decision,eligible.Code);
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)return new(InvoiceApiDecision.Forbidden,"access_denied");
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1).Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId).SingleOrDefaultAsync(ct);
        if(owner is null)return new(InvoiceApiDecision.NotFound,"not_found");
        var subscription=await subscriptions.CheckAsync(owner.Value,ct);
        if(subscription is not null)return new(InvoiceApiDecision.Conflict,subscription);
        var sourceDispatch=await dispatch.GetAsync(bearer,taxpayerId,predecessorId,ct);
        if(sourceDispatch.Overview is not{Status:"Acknowledged",Environment:"Production",ReferenceNumber:not null} predecessorOperation)
            return new(InvoiceApiDecision.Conflict,"accepted_predecessor_required");
        var source=await db.InvoiceDocuments.AsNoTracking().SingleOrDefaultAsync(i=>i.Id==predecessorId&&i.TaxpayerId==taxpayerId&&i.CustomerUserId==owner.Value,ct);
        if(source is null)return new(InvoiceApiDecision.NotFound,"not_found");
        var successorId=Guid.NewGuid();var now=DateTime.UtcNow;
        var root=JsonNode.Parse(source.SnapshotJson)?.AsObject();
        if(root is null||(string?)root["environment"]!="Production"||(int?)root["invoiceType"]!=1||(int?)root["pattern"]!=1)
            return new(InvoiceApiDecision.Invalid,"unsupported_predecessor_profile");
        root["sourceWorkspaceId"]=successorId.ToString("D");root["sourceRevision"]=1;root["issueDateUtc"]=now;
        root["referenceTaxId"]=predecessorTaxId;root["predecessorInvoiceId"]=predecessorId.ToString("D");root["legalSubject"]=3;
        var json=root.ToJsonString();var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        try
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
                await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
                db.InvoiceDocuments.Add(new StoredInvoice{Id=successorId,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,Revision=1,
                    Subject=InvoiceSubject.Cancellation,State=InvoiceLifecycleState.Finalized,IssueDateUtc=now,SnapshotJson=json,SnapshotSha256=hash,UpdatedAtUtc=now});
                db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision{InvoiceId=successorId,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,
                    Revision=1,SnapshotJson=json,SnapshotSha256=hash,CapturedAtUtc=now});
                db.InvoiceReviews.Add(new StoredInvoiceReview{InvoiceId=successorId,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,
                    State=InvoiceReviewState.Approved,Revision=1,UpdatedAtUtc=now,LastActorUserId=actor.ActorId});
                await db.SaveChangesAsync(ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO authority_invoice_legal_relations
(id,customer_user_id,taxpayer_id,predecessor_invoice_id,successor_invoice_id,predecessor_fiscal_tax_id,
 predecessor_operation_id,subject,state,idempotency_key,evidence_reference,created_at_utc)
VALUES({Guid.NewGuid()},{owner.Value},{taxpayerId},{predecessorId},{successorId},{predecessorTaxId},
 {predecessorOperation.OperationId},3,1,{"cancel:"+predecessorId.ToString("N")},{"accepted_inquiry:"+predecessorOperation.OperationId.ToString("N")},{now})
""",ct);
                await tx.CommitAsync(ct);
            });
        }
        catch(PostgresException ex)when(ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {return new(InvoiceApiDecision.Conflict,"cancellation_already_requested");}
        catch(DbUpdateException ex)when(ex.InnerException is PostgresException{SqlState:PostgresErrorCodes.UniqueViolation})
        {return new(InvoiceApiDecision.Conflict,"cancellation_already_requested");}
        var sent=await submission.SendAsync(bearer,taxpayerId,successorId,ct);
        return new(sent.Decision,sent.Code,successorId,sent.Status,sent.ReferenceNumber,sent.InquiryCode);
    }
}
