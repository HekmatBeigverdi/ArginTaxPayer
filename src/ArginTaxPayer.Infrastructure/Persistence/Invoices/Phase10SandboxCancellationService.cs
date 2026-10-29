using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record CancellationSendResult(InvoiceApiDecision Decision,string Code,Guid? InvoiceId=null,
 string? Status=null,string? ReferenceNumber=null,string? InquiryCode=null);

public sealed class Phase10SandboxCancellationService(
 ArginTaxPayerDbContext db,TaxpayerScopeService scope,
 Phase10CancellationEligibilityService eligibility,Phase10OneClickSandboxSubmissionService submission)
{
 public async Task<CancellationSendResult> SendAsync(string? bearer,Guid taxpayerId,Guid predecessorId,CancellationToken ct)
 {
  var actor=await scope.ResolveAsync(bearer,ct);
  if(actor.Decision!=TaxpayerScopeDecision.Allowed)
   return new(actor.Decision==TaxpayerScopeDecision.Unauthenticated?
    InvoiceApiDecision.Unauthenticated:InvoiceApiDecision.Forbidden,"access_denied");
  var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
   .Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId).SingleOrDefaultAsync(ct);
  if(owner is null)return new(InvoiceApiDecision.NotFound,"not_found");
  var eligible=await eligibility.ReadAsync(bearer,taxpayerId,predecessorId,ct);
  if(!eligible.Eligible||eligible.FiscalTaxId is not {Length:22} predecessorTaxId)
   return new(eligible.Decision,eligible.Code);
  var successor=Guid.NewGuid();
  try{
   var transactionResult = await db.Database.CreateExecutionStrategy().ExecuteAsync<CancellationSendResult?>(async () => {
   await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
   var current=await db.InvoiceDocuments.AsNoTracking().SingleOrDefaultAsync(i=>
    i.Id==predecessorId&&i.TaxpayerId==taxpayerId&&i.CustomerUserId==owner.Value,ct);
   if(current is null)return new(InvoiceApiDecision.NotFound,"not_found");
   if(current.Subject!=InvoiceSubject.Original||current.State!=InvoiceLifecycleState.Finalized)
    return new(InvoiceApiDecision.Conflict,"invalid_predecessor");
   if(await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").SingleAsync(ct)!="argintaxpayer_test")
    return new(InvoiceApiDecision.Forbidden,"sandbox_database_required");
   var existing=await db.Database.SqlQueryRaw<int>($"""
SELECT 1 AS "Value" FROM sandbox_invoice_legal_relations
WHERE customer_user_id='{owner.Value}' AND taxpayer_id='{taxpayerId}'
 AND predecessor_invoice_id='{predecessorId}' AND subject=3 AND state IN (1,2,4) LIMIT 1
""").AnyAsync(ct);
   if(existing)return new(InvoiceApiDecision.Conflict,"cancellation_already_requested");
   var snapshot=JsonNode.Parse(current.SnapshotJson)?.AsObject();
   if(snapshot is null||(string?)snapshot["environment"]!="Sandbox"||
      (int?)snapshot["invoiceType"]!=1||(int?)snapshot["pattern"]!=1)
    return new(InvoiceApiDecision.Invalid,"unsupported_predecessor_profile");
   var now=DateTime.UtcNow;
   snapshot["sourceWorkspaceId"]=successor.ToString("D");
   snapshot["sourceRevision"]=1;
   snapshot["issueDateUtc"]=now;
   snapshot["referenceTaxId"]=predecessorTaxId;
   snapshot["predecessorInvoiceId"]=predecessorId.ToString("D");
   snapshot["legalSubject"]=3;
   var json=snapshot.ToJsonString();
   var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
   db.InvoiceDocuments.Add(new StoredInvoice{
    Id=successor,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,Revision=1,
    Subject=InvoiceSubject.Cancellation,State=InvoiceLifecycleState.Finalized,
    IssueDateUtc=now,SnapshotJson=json,SnapshotSha256=hash,UpdatedAtUtc=now});
   db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision{
    InvoiceId=successor,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,Revision=1,
    SnapshotJson=json,SnapshotSha256=hash,CapturedAtUtc=now});
   db.InvoiceReviews.Add(new StoredInvoiceReview{
    InvoiceId=successor,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,
    State=InvoiceReviewState.Approved,Revision=1,UpdatedAtUtc=now,LastActorUserId=actor.ActorId});
   await db.SaveChangesAsync(ct);
   await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO sandbox_invoice_legal_relations
(id,customer_user_id,taxpayer_id,predecessor_invoice_id,successor_invoice_id,
predecessor_fiscal_tax_id,subject,state,idempotency_key,evidence_reference,created_at_utc)
VALUES ({Guid.NewGuid()},{owner.Value},{taxpayerId},{predecessorId},{successor},
{predecessorTaxId},3,1,{"cancel:"+predecessorId.ToString("N")},{"user-confirmed"},{now})
""",ct);
   await tx.CommitAsync(ct);
   return null;
   });
   if(transactionResult is not null)return transactionResult;
  }catch(DbUpdateException ex)when(ex.InnerException is PostgresException {SqlState:PostgresErrorCodes.UniqueViolation}){
   return new(InvoiceApiDecision.Conflict,"cancellation_already_requested");
  }catch(PostgresException ex)when(ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure){
   return new(InvoiceApiDecision.Conflict,"cancellation_already_requested");
  }
  var result=await submission.SendAsync(bearer,taxpayerId,successor,ct);
  return new(result.Decision,result.Code,successor,result.Status,result.ReferenceNumber,result.InquiryCode);
 }
}
