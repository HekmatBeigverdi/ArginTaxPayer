using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record CorrectionEditLine(decimal Quantity, decimal UnitPriceRial, decimal DiscountPerUnitRial);
public sealed record CorrectionSaveRequest(long ExpectedRevision,CorrectionEditLine[] Lines,
 int SettlementMethod,decimal CashAmountRial,decimal CreditAmountRial,
 bool ReductionCorrectsOriginalMistake=false);
public sealed record CorrectionDraftView(Guid WorkspaceId,Guid PredecessorInvoiceId,long Revision,
 InvoiceEntryLine[] Lines,int SettlementMethod,decimal CashAmountRial,decimal CreditAmountRial);
public sealed record CorrectionOperationResult(InvoiceApiDecision Decision,string Code,
 CorrectionDraftView? Draft=null,Guid? InvoiceId=null,string? Status=null,
 string? ReferenceNumber=null,string? InquiryCode=null);

/// <summary>Independent V7.9 correction draft, immutable predecessor, one send operation.</summary>
public sealed class Phase10SandboxCorrectionService(ArginTaxPayerDbContext db,
 TaxpayerScopeService scope,Phase10LegacyReferenceInquiryService inquiry,
 Phase10OneClickSandboxSubmissionService sender)
{
 private async Task<Guid?> Owner(string? bearer,Guid taxpayerId,CancellationToken ct)
 {
  var actor=await scope.ResolveAsync(bearer,ct);
  if(actor.Decision!=TaxpayerScopeDecision.Allowed)return null;
  return await scope.ScopedQuery(actor.ActorId,actor.Level1)
   .Where(x=>x.Id==taxpayerId).Select(x=>(Guid?)x.CustomerUserId).SingleOrDefaultAsync(ct);
 }
 private async Task<(Guid Id,long Revision,string Json)?> Find(Guid owner,Guid taxpayer,
  Guid predecessor,CancellationToken ct)
 {
  var conn=db.Database.GetDbConnection();
  var open=conn.State!=System.Data.ConnectionState.Open;
  if(open)await conn.OpenAsync(ct);
  try{
   await using var c=conn.CreateCommand();
   c.CommandText="""
SELECT id,revision,draft_json::text FROM sandbox_invoice_correction_workspaces
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer
 AND predecessor_invoice_id=@predecessor AND state=1
ORDER BY created_at_utc DESC LIMIT 1
""";
   foreach(var (n,v) in new (string,object)[]{
    ("owner",owner),("taxpayer",taxpayer),("predecessor",predecessor)})
   {var p=c.CreateParameter();p.ParameterName=n;p.Value=v;c.Parameters.Add(p);}
   await using var reader=await c.ExecuteReaderAsync(ct);
   return await reader.ReadAsync(ct)?(reader.GetGuid(0),reader.GetInt64(1),reader.GetString(2)):null;
  }finally{if(open)await conn.CloseAsync();}
 }
 private static CorrectionDraftView View(Guid id,Guid parent,long revision,string json)
 {
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  var lines=JsonSerializer.Deserialize<InvoiceEntryLine[]>(root.GetProperty("lines").GetRawText())??[];
  return new(id,parent,revision,lines,root.GetProperty("settlementMethod").GetInt32(),
   root.GetProperty("cashAmountRial").GetDecimal(),root.GetProperty("creditAmountRial").GetDecimal());
 }
 public async Task<CorrectionOperationResult> OpenAsync(string? bearer,Guid taxpayerId,
  Guid predecessorId,CancellationToken ct)
 {
  var owner=await Owner(bearer,taxpayerId,ct);
  if(owner is null)return new(InvoiceApiDecision.Forbidden,"not_authorized");
  var current=await Find(owner.Value,taxpayerId,predecessorId,ct);
  if(current is { } exists)return new(InvoiceApiDecision.Success,"existing_draft",
   View(exists.Id,predecessorId,exists.Revision,exists.Json));
  var original=await db.InvoiceDocuments.AsNoTracking().FirstOrDefaultAsync(x=>
   x.Id==predecessorId&&x.TaxpayerId==taxpayerId&&x.CustomerUserId==owner.Value,ct);
  if(original is null)return new(InvoiceApiDecision.NotFound,"not_found");
  if(original.State!=InvoiceLifecycleState.Finalized||original.Subject==InvoiceSubject.Cancellation)
   return new(InvoiceApiDecision.Conflict,"invalid_predecessor");
  var official=await inquiry.ReadAsync(bearer,taxpayerId,predecessorId,ct);
  if(official.Decision!=InvoiceApiDecision.Success||official.AuthorityResult?.Status!="SUCCESS")
   return new(InvoiceApiDecision.Conflict,"accepted_predecessor_required");
  var taxid=await db.Database.SqlQueryRaw<string>($"""
SELECT fiscal_tax_id AS "Value" FROM sandbox_invoice_fiscal_reservations
WHERE customer_user_id='{owner.Value}' AND taxpayer_id='{taxpayerId}'
 AND invoice_id='{predecessorId}' LIMIT 1
""").FirstOrDefaultAsync(ct);
  if(taxid is not {Length:22})return new(InvoiceApiDecision.Conflict,"reference_taxid_missing");
  if(await db.Database.SqlQueryRaw<int>($"""
SELECT 1 AS "Value" FROM sandbox_invoice_legal_relations
WHERE customer_user_id='{owner.Value}' AND taxpayer_id='{taxpayerId}'
 AND predecessor_invoice_id='{predecessorId}' AND subject IN (2,4)
 AND state IN (1,2,4) LIMIT 1
""").AnyAsync(ct))return new(InvoiceApiDecision.Conflict,"active_related_invoice");
  var snapshot=JsonNode.Parse(original.SnapshotJson)?.AsObject();
  if(snapshot is null||(string?)snapshot["environment"]!="Sandbox"||
   (int?)snapshot["invoiceType"]!=1||(int?)snapshot["pattern"]!=1)
   return new(InvoiceApiDecision.Invalid,"unsupported_pattern");
  var id=Guid.NewGuid();var now=DateTime.UtcNow;
  snapshot["referenceTaxId"]=taxid;
  snapshot["predecessorInvoiceId"]=predecessorId.ToString("D");
  snapshot["legalSubject"]=2;
  var json=snapshot.ToJsonString();
  try{
   await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO sandbox_invoice_correction_workspaces
(id,customer_user_id,taxpayer_id,predecessor_invoice_id,predecessor_fiscal_tax_id,
 revision,draft_json,state,created_at_utc,updated_at_utc)
VALUES({id},{owner.Value},{taxpayerId},{predecessorId},{taxid},
 1,CAST({json} AS jsonb),1,{now},{now})
""",ct);
  }catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.UniqueViolation){
   current=await Find(owner.Value,taxpayerId,predecessorId,ct);
   if(current is { } found)return new(InvoiceApiDecision.Success,"existing_draft",
    View(found.Id,predecessorId,found.Revision,found.Json));
   return new(InvoiceApiDecision.Conflict,"draft_conflict");
  }
  return new(InvoiceApiDecision.Success,"draft_created",View(id,predecessorId,1,json));
 }
 public async Task<CorrectionOperationResult> ReadAsync(string? bearer,Guid taxpayerId,
  Guid predecessorId,CancellationToken ct)
 {
  var owner=await Owner(bearer,taxpayerId,ct);
  if(owner is null)return new(InvoiceApiDecision.Forbidden,"not_authorized");
  var draft=await Find(owner.Value,taxpayerId,predecessorId,ct);
  return draft is { } d?new(InvoiceApiDecision.Success,"draft_loaded",
   View(d.Id,predecessorId,d.Revision,d.Json)):
   new(InvoiceApiDecision.NotFound,"draft_not_found");
 }
 public async Task<CorrectionOperationResult> SaveAsync(string? bearer,Guid taxpayerId,
  Guid predecessorId,CorrectionSaveRequest input,CancellationToken ct)
 {
  var owner=await Owner(bearer,taxpayerId,ct);
  if(owner is null)return new(InvoiceApiDecision.Forbidden,"not_authorized");
  var draft=await Find(owner.Value,taxpayerId,predecessorId,ct);
  if(draft is not { } d)return new(InvoiceApiDecision.NotFound,"draft_not_found");
  if(d.Revision!=input.ExpectedRevision)return new(InvoiceApiDecision.Conflict,"stale_revision");
  var source=await db.InvoiceDocuments.AsNoTracking().FirstAsync(x=>
   x.Id==predecessorId&&x.TaxpayerId==taxpayerId&&x.CustomerUserId==owner.Value,ct);
  using var originalDoc=JsonDocument.Parse(source.SnapshotJson);
  var baseline=JsonSerializer.Deserialize<InvoiceEntryLine[]>(
   originalDoc.RootElement.GetProperty("lines").GetRawText())??[];
  var edited=baseline.Select((x,i)=>i<input.Lines.Length?x with{
   Quantity=input.Lines[i].Quantity,UnitPriceRial=input.Lines[i].UnitPriceRial,
   DiscountPerUnitRial=input.Lines[i].DiscountPerUnitRial}:x).ToArray();
  if(input.Lines.Length!=baseline.Length||
   !Phase10CorrectionValidation.AllowedLines(baseline,edited,input.ReductionCorrectsOriginalMistake))
   return new(InvoiceApiDecision.Invalid,"illegal_correction_fields");
  if(edited.Any(x=>x.Quantity*x.UnitPriceRial!=decimal.Truncate(x.Quantity*x.UnitPriceRial)||
   x.Quantity*x.DiscountPerUnitRial!=decimal.Truncate(x.Quantity*x.DiscountPerUnitRial)))
   return new(InvoiceApiDecision.Invalid,"non_integral_rial");
  InvoiceFinancialPreview financial;
  try{
   financial=InvoiceFinancialCalculator.Preview(edited.Select(x=>new InvoiceFinancialInput(
    x.CatalogItemId??Guid.Empty,x.Quantity,x.UnitPriceRial,
    decimal.Truncate(x.Quantity*x.DiscountPerUnitRial),x.VatRatePercent)).ToArray(),
    new InvoiceTypePattern(ElectronicInvoiceType.First,1),
    new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition));
  }catch(ArgumentException){return new(InvoiceApiDecision.Invalid,"invalid_amounts");}
  var extra=originalDoc.RootElement.GetProperty("otherTaxAndLegalFeesRial").GetDecimal();
  var total=(financial.GrandTotalRial??0m)+extra;
  if(input.CashAmountRial<0||input.CreditAmountRial<0||
   input.CashAmountRial+input.CreditAmountRial!=total||
   (input.SettlementMethod==1 && input.CreditAmountRial!=0)||
   (input.SettlementMethod==2 && input.CashAmountRial!=0)||
   (input.SettlementMethod==3 && (input.CashAmountRial<=0||input.CreditAmountRial<=0))||
   input.SettlementMethod is <1 or >3)
   return new(InvoiceApiDecision.Invalid,"settlement_invalid");
  var root=JsonNode.Parse(d.Json)!.AsObject();
  root["lines"]=JsonSerializer.SerializeToNode(edited);
  root["settlementMethod"]=input.SettlementMethod;
  root["cashAmountRial"]=input.CashAmountRial;
  root["creditAmountRial"]=input.CreditAmountRial;
  root["financialSummary"]=JsonSerializer.SerializeToNode(new InvoiceDraftFinancialSummary(
   financial.BeforeDiscountRial,financial.DiscountRial,financial.AfterDiscountRial,
   financial.VatRial??0m,financial.GrandTotalRial??0m));
  root["reductionCorrectsOriginalMistake"]=input.ReductionCorrectsOriginalMistake;
  var json=root.ToJsonString();var now=DateTime.UtcNow;
  var rows=await db.Database.ExecuteSqlInterpolatedAsync($"""
UPDATE sandbox_invoice_correction_workspaces
SET draft_json=CAST({json} AS jsonb),revision=revision+1,updated_at_utc={now}
WHERE id={d.Id} AND customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
 AND predecessor_invoice_id={predecessorId} AND revision={d.Revision} AND state=1
""",ct);
  return rows==1?new(InvoiceApiDecision.Success,"draft_saved",
   View(d.Id,predecessorId,d.Revision+1,json)):
   new(InvoiceApiDecision.Conflict,"concurrent_change");
 }
 public async Task<CorrectionOperationResult> DiscardAsync(string? bearer,Guid taxpayerId,
  Guid predecessorId,CancellationToken ct)
 {
  var owner=await Owner(bearer,taxpayerId,ct);
  if(owner is null)return new(InvoiceApiDecision.Forbidden,"not_authorized");
  var rows=await db.Database.ExecuteSqlInterpolatedAsync($"""
UPDATE sandbox_invoice_correction_workspaces SET state=3,updated_at_utc={DateTime.UtcNow}
WHERE customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
 AND predecessor_invoice_id={predecessorId} AND state=1
""",ct);
  return new(InvoiceApiDecision.Success,rows>0?"draft_discarded":"nothing_to_discard");
 }
 public async Task<CorrectionOperationResult> SubmitAsync(string? bearer,Guid taxpayerId,
  Guid predecessorId,CancellationToken ct)
 {
  var actor=await scope.ResolveAsync(bearer,ct);
  var owner=await Owner(bearer,taxpayerId,ct);
  if(owner is null)return new(InvoiceApiDecision.Forbidden,"not_authorized");
  var draft=await Find(owner.Value,taxpayerId,predecessorId,ct);
  if(draft is not { } d)return new(InvoiceApiDecision.NotFound,"draft_not_found");
  var accepted=await inquiry.ReadAsync(bearer,taxpayerId,predecessorId,ct);
  if(accepted.AuthorityResult?.Status!="SUCCESS")
   return new(InvoiceApiDecision.Conflict,"predecessor_not_accepted");
  var original=await db.InvoiceDocuments.AsNoTracking().FirstAsync(x=>
   x.Id==predecessorId&&x.TaxpayerId==taxpayerId&&x.CustomerUserId==owner.Value,ct);
  var root=JsonNode.Parse(d.Json)!.AsObject();
  using var origDoc=JsonDocument.Parse(original.SnapshotJson);
  var originalLines=JsonSerializer.Deserialize<InvoiceEntryLine[]>(
   origDoc.RootElement.GetProperty("lines").GetRawText())??[];
  var changedLines=JsonSerializer.Deserialize<InvoiceEntryLine[]>(
   root["lines"]!.ToJsonString())??[];
  var reduced=(bool?)root["reductionCorrectsOriginalMistake"]??false;
  if(!Phase10CorrectionValidation.AllowedLines(originalLines,changedLines,reduced))
   return new(InvoiceApiDecision.Invalid,"invalid_correction_lines");
  var now=DateTime.UtcNow;var invoiceId=Guid.NewGuid();
  root["sourceWorkspaceId"]=invoiceId.ToString("D");
  root["sourceRevision"]=1;
  root["issueDateUtc"]=now;
  root["legalSubject"]=2;
  var json=root.ToJsonString();
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
  try{
   await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
   var updated=await db.Database.ExecuteSqlInterpolatedAsync($"""
UPDATE sandbox_invoice_correction_workspaces SET state=2,updated_at_utc={now}
WHERE id={d.Id} AND revision={d.Revision} AND state=1
 AND customer_user_id={owner.Value} AND taxpayer_id={taxpayerId}
""",ct);
   if(updated!=1)return new(InvoiceApiDecision.Conflict,"draft_changed");
   db.InvoiceDocuments.Add(new StoredInvoice{
    Id=invoiceId,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,
    Revision=1,State=InvoiceLifecycleState.Finalized,Subject=InvoiceSubject.Correction,
    IssueDateUtc=now,SnapshotJson=json,SnapshotSha256=hash,UpdatedAtUtc=now});
   db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision{
    InvoiceId=invoiceId,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,
    Revision=1,SnapshotJson=json,SnapshotSha256=hash,CapturedAtUtc=now});
   db.InvoiceReviews.Add(new StoredInvoiceReview{
    InvoiceId=invoiceId,CustomerUserId=owner.Value,TaxpayerId=taxpayerId,
    Revision=1,State=InvoiceReviewState.Approved,
    UpdatedAtUtc=now,LastActorUserId=actor.ActorId});
   await db.SaveChangesAsync(ct);
   var reference=(string?)root["referenceTaxId"]??"";
   await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO sandbox_invoice_legal_relations
(id,customer_user_id,taxpayer_id,predecessor_invoice_id,successor_invoice_id,
predecessor_fiscal_tax_id,subject,state,idempotency_key,evidence_reference,created_at_utc)
VALUES({Guid.NewGuid()},{owner.Value},{taxpayerId},{predecessorId},{invoiceId},
 {reference},2,1,{"correction:"+d.Id.ToString("N")},{"user-confirmed"},{now})
""",ct);
   await tx.CommitAsync(ct);
  }catch(PostgresException ex)when(ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure){
   return new(InvoiceApiDecision.Conflict,"correction_conflict");
  }
  var result=await sender.SendAsync(bearer,taxpayerId,invoiceId,ct);
  return new(result.Decision,result.Code,null,invoiceId,result.Status,
   result.ReferenceNumber,result.InquiryCode);
 }
}
