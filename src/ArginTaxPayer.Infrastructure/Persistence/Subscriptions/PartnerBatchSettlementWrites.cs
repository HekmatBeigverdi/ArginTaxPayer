using ArginTaxPayer.Domain.Subscriptions;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
public sealed partial class PartnerBatchSettlementService
{
 public async Task<PartnerBatchSummary> RegisterAsync(string? bearer,PartnerBatchRegisterRequest request,CancellationToken ct=default)
 {
  CheckPartner(request.PartnerUserId);Evidence(request.OperationKey,request.Reference,request.Note);
  if(string.IsNullOrWhiteSpace(request.PreviewFingerprint)||request.PreviewFingerprint.Length>128)throw new ArgumentException("Preview required.");
  var s=await Authorize(bearer,true,ct);var root=await ResolvePartner(s,request.PartnerUserId,ct);
  var hash=Hash("batch:"+Serialize(request));
  return await Atomic(async()=>
  {
   await Lock(root,request.PartnerUserId,ct);
   // The same operation key with exactly the same submitted payload replays its immutable response.
   var prior=await Strings("SELECT jsonb_build_object('hash',request_hash,'snapshot',snapshot)::text FROM partner_settlement_batches WHERE root_user_id=@root AND operation_key=@op",ct,("root",root),("op",request.OperationKey));
   if(prior.Count>0)
   {
    using var json=System.Text.Json.JsonDocument.Parse(prior[0]);
    if(json.RootElement.GetProperty("hash").GetString()!=hash)throw new InvalidOperationException("Operation key payload conflict.");
    return Decode<PartnerBatchSummary>(json.RootElement.GetProperty("snapshot").GetRawText());
   }
   if((await Strings("SELECT id::text FROM partner_settlement_expenses WHERE root_user_id=@root AND operation_key=@op",ct,("root",root),("op",request.OperationKey))).Count>0)throw new InvalidOperationException("Operation key conflict.");
   await LockReceipts(root,request.PartnerUserId,null,ct);
   var preview=await Preview(root,request.PartnerUserId,1,ct,true);
   if(preview.PreviewFingerprint!=request.PreviewFingerprint)throw new InvalidOperationException("Preview changed. Refresh before posting.");
   if(!preview.CanRegister)throw new InvalidOperationException("No eligible receipt lines or missing approved terms.");
   var id=Guid.NewGuid();var now=DateTime.UtcNow;
   var partnerName=await DisplayName(request.PartnerUserId,ct);var rootName=await DisplayName(root,ct);
   var summary=new PartnerBatchSummary(id,"BS-"+now.ToString("yyyyMMdd")+"-"+id.ToString("N")[..12].ToUpperInvariant(),request.PartnerUserId,partnerName,rootName,now,request.Reference.Trim(),request.Note.Trim(),preview.TotalCollectedIrr,preview.CashIrr,preview.BankIrr,preview.TotalCostIrr,preview.RootPaidCostIrr,preview.TotalPartnerShareIrr,preview.TotalRootShareIrr,preview.NetToRootIrr,preview.TotalCount);
   await Execute("""
   INSERT INTO partner_settlement_batches(id,root_user_id,partner_user_id,operation_key,request_hash,created_at_utc,snapshot)
   VALUES(@id,@root,@partner,@op,@hash,@now,CAST(@snapshot AS jsonb))
   """,ct,("id",id),("root",root),("partner",request.PartnerUserId),("op",request.OperationKey),("hash",hash),("now",now),("snapshot",Serialize(summary)));
   foreach(var line in preview.Items)
   {
    // Validate the independently calculated SQL amounts with the common domain policy.
    var math=PartnerBatchSettlementPolicy.Calculate(line.GrossIrr,line.ApprovedCostIrr,line.RootPaidCostIrr,line.PartnerBasisPoints!.Value);
    if(math.NetToRootIrr!=line.NetToRootIrr||math.PartnerShareIrr!=line.PartnerShareIrr)throw new InvalidOperationException("Receipt arithmetic mismatch.");
    await Execute("""
    INSERT INTO partner_settlement_batch_lines(id,batch_id,collection_id,root_user_id,partner_user_id,partner_basis_points,
    gross_irr,cost_irr,root_paid_cost_irr,partner_share_irr,root_share_irr,net_to_root_irr,
    previous_gross_irr,previous_cost_irr,previous_root_paid_cost_irr,previous_partner_share_irr,previous_root_share_irr,previous_net_to_root_irr,created_at_utc,snapshot)
    SELECT @id,@batch,@collection,@root,@partner,@rate,@gross,@cost,@rootcost,@partnershare,@rootshare,@net,
    @prevgross,@prevcost,COALESCE((SELECT root_paid_cost_irr FROM partner_settlement_batch_lines WHERE collection_id=@collection ORDER BY created_at_utc DESC,id DESC LIMIT 1),0),@prevpartner,@prevroot,@prevnet,@now,CAST(@snapshot AS jsonb)
    """,ct,("id",Guid.NewGuid()),("batch",id),("collection",line.CollectionId),("root",root),("partner",request.PartnerUserId),("rate",line.PartnerBasisPoints.Value),
    ("gross",line.GrossIrr),("cost",line.ApprovedCostIrr),("rootcost",line.RootPaidCostIrr),("partnershare",line.PartnerShareIrr),("rootshare",line.RootShareIrr),("net",line.NetToRootIrr),
    ("prevgross",line.GrossIrr-line.DeltaCollectedIrr),("prevcost",line.ApprovedCostIrr-line.DeltaCostIrr),("prevpartner",line.PartnerShareIrr-line.DeltaPartnerShareIrr),("prevroot",line.RootShareIrr-line.DeltaRootShareIrr),("prevnet",line.PreviousNetToRootIrr),("now",now),("snapshot",Serialize(line)));
   }
   return summary;
  },ct);
 }
 public async Task<Guid> AddExpenseAsync(string? bearer,PartnerBatchExpenseRequest request,CancellationToken ct=default)
 {
  CheckPartner(request.PartnerUserId);Evidence(request.OperationKey,request.Reference,request.Note);
  if(request.CollectionId==Guid.Empty||request.AmountIrr<=0)throw new ArgumentException("Positive receipt expense required.");
  var s=await Authorize(bearer,true,ct);var root=await ResolvePartner(s,request.PartnerUserId,ct);var hash=Hash("expense:"+Serialize(request));
  return await Atomic(async()=>
  {
   await Lock(root,request.PartnerUserId,ct);
   var prior=await Strings("SELECT jsonb_build_object('id',id,'hash',request_hash)::text FROM partner_settlement_expenses WHERE root_user_id=@root AND operation_key=@op",ct,("root",root),("op",request.OperationKey));
   if(prior.Count>0)
   {
    using var json=System.Text.Json.JsonDocument.Parse(prior[0]);if(json.RootElement.GetProperty("hash").GetString()!=hash)throw new InvalidOperationException("Operation key payload conflict.");
    return json.RootElement.GetProperty("id").GetGuid();
   }
   if((await Strings("SELECT id::text FROM partner_settlement_batches WHERE root_user_id=@root AND operation_key=@op",ct,("root",root),("op",request.OperationKey))).Count>0)throw new InvalidOperationException("Operation key conflict.");
   await LockReceipts(root,request.PartnerUserId,request.CollectionId,ct);
   var receipts=await Strings(CandidateSql+"SELECT jsonb_build_object('gross',amount_irr,'cost',cost,'channel',channel,'legacy',legacy_conflict)::text FROM scoped WHERE id=@collection",ct,("root",root),("partner",request.PartnerUserId),("collection",request.CollectionId));
   if(receipts.Count==0)throw new UnauthorizedAccessException("Receipt outside selected partner scope.");
   using var receipt=System.Text.Json.JsonDocument.Parse(receipts[0]);var r=receipt.RootElement;
   if(r.GetProperty("channel").GetInt32()==3||r.GetProperty("legacy").GetBoolean())throw new InvalidOperationException("Receipt requires ownership or legacy reconciliation.");
   if(request.AmountIrr>r.GetProperty("gross").GetInt64()-r.GetProperty("cost").GetInt64())throw new InvalidOperationException("Total costs exceed collected receipt.");
   var id=Guid.NewGuid();
   await Execute("""
   INSERT INTO partner_settlement_expenses(id,collection_id,root_user_id,partner_user_id,amount_irr,paid_by_level1,reference,note,operation_key,request_hash,approved_at_utc)
   VALUES(@id,@collection,@root,@partner,@amount,@payer,@reference,@note,@op,@hash,@now)
   """,ct,("id",id),("collection",request.CollectionId),("root",root),("partner",request.PartnerUserId),("amount",request.AmountIrr),("payer",request.PaidByLevel1),("reference",request.Reference.Trim()),("note",request.Note.Trim()),("op",request.OperationKey),("hash",hash),("now",DateTime.UtcNow));
   return id;
  },ct);
 }
 private async Task<string> DisplayName(Guid user,CancellationToken ct)=>(await Strings("SELECT COALESCE(NULLIF(trim(p.company_name),''),NULLIF(trim(p.first_name||' '||p.last_name),''),u.normalized_email,u.normalized_mobile,u.id::text) FROM identity_users u LEFT JOIN software_user_profiles p ON p.user_id=u.id WHERE u.id=@id",ct,("id",user)))[0];
 private async Task LockReceipts(Guid root,Guid partner,Guid? collection,CancellationToken ct)
 {
  // Stable receipt order avoids deadlocks when a batch covers many orders.
  await Execute(CandidateSql+"SELECT c.id FROM commercial_collections c JOIN scoped s ON s.id=c.id WHERE (@collection::uuid IS NULL OR c.id=@collection) ORDER BY c.id FOR UPDATE OF c",ct,("root",root),("partner",partner),("collection",collection));
 }
}
