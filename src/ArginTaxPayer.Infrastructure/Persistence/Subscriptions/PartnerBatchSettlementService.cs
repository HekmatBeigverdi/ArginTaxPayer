using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Immutable receipt settlement vouchers; posting records transfer evidence only.</summary>
public sealed partial class PartnerBatchSettlementService(ArginTaxPayerDbContext db,IdentityAuthorizationService identity)
{
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 private static string Serialize<T>(T value)=>JsonSerializer.Serialize(value,Json);
 private static T Decode<T>(string value)=>JsonSerializer.Deserialize<T>(value,Json)!;
 private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
 private sealed record Scope(Guid Actor,bool IsRoot);
 private async Task<Scope> Authorize(string? token,bool write,CancellationToken ct)
 {
  var actor=await identity.CurrentUserIdAsync(token,ct)??throw new UnauthorizedAccessException();
  var roles=await identity.GetCurrentRolesAsync(token,ct)??[];
  var root=roles.Contains(IdentityRoleNames.Level1);
  if((!root&&!roles.Contains(IdentityRoleNames.Level2))||!await db.IdentityUsers.AsNoTracking().AnyAsync(x=>x.Id==actor&&x.IsActive,ct))throw new UnauthorizedAccessException();
  if(write&&(!root||!await db.IdentityMfaStates.AnyAsync(x=>x.UserId==actor&&x.EnabledAtUtc!=null,ct)))throw new UnauthorizedAccessException();
  return new(actor,root);
 }
 private async Task Open(CancellationToken ct){if(db.Database.GetDbConnection().State!=ConnectionState.Open)await db.Database.OpenConnectionAsync(ct);}
 private DbCommand Command(string sql,params (string Name,object? Value)[] values)
 {
  var c=db.Database.GetDbConnection().CreateCommand();c.CommandText=sql;
  c.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
  foreach(var v in values){var p=c.CreateParameter();p.ParameterName=v.Name;p.Value=v.Value??DBNull.Value;c.Parameters.Add(p);}return c;
 }
 private async Task<List<string>> Strings(string sql,CancellationToken ct,params (string Name,object? Value)[] values)
 {
  await Open(ct);await using var cmd=Command(sql,values);await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<string>();while(await r.ReadAsync(ct))result.Add(r.GetString(0));return result;
 }
 private async Task Execute(string sql,CancellationToken ct,params (string Name,object? Value)[] values)
 {await Open(ct);await using var c=Command(sql,values);await c.ExecuteNonQueryAsync(ct);}
 private async Task<T> Atomic<T>(Func<Task<T>> action,CancellationToken ct)
 {
  // Joining an existing serializable unit of work also supports rollback-only operator tooling.
  if(db.Database.CurrentTransaction is { } outer)
  {
   if(outer.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)throw new InvalidOperationException("A serializable transaction is required.");
   var savepoint="partner_batch_"+Guid.NewGuid().ToString("N");await outer.CreateSavepointAsync(savepoint,ct);
   try{var result=await action();await outer.ReleaseSavepointAsync(savepoint,ct);return result;}
   catch{await outer.RollbackToSavepointAsync(savepoint,CancellationToken.None);throw;}
  }
  return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
  {
   await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
   var result=await action();await tx.CommitAsync(ct);return result;
  });
 }
 private async Task Lock(Guid root,Guid partner,CancellationToken ct)
 {
  // A partner-wide lock also serializes ownership corrections across root scopes.
  await Execute("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",ct,("key","partner-batch:"+partner));
  await Execute("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",ct,("key","partner-batch-root:"+root));
 }
 private static void CheckPartner(Guid partner){if(partner==Guid.Empty)throw new ArgumentException("Select a partner.");}
 private static void Evidence(Guid operation,string reference,string note)
 {if(operation==Guid.Empty||string.IsNullOrWhiteSpace(reference)||reference.Length>160||note is null||note.Length>1000)throw new ArgumentException("Invalid transfer evidence.");}
 private async Task<Guid> ResolvePartner(Scope scope,Guid partner,CancellationToken ct)
 {
  CheckPartner(partner);if(!scope.IsRoot&&scope.Actor!=partner)throw new UnauthorizedAccessException();
  var list=await PartnerRoots(scope,partner,ct);
  if(list.Count==0)throw new UnauthorizedAccessException("Partner outside scope.");
  if(list.Count>1)
  {
   var current=await Strings("SELECT DISTINCT root_level1_user_id::text FROM customer_commercial_owners WHERE partner_level2_user_id=@partner",ct,("partner",partner));
   if(current.Count==1)return Guid.Parse(current[0]);
   throw new InvalidOperationException("Partner has multiple root scopes.");
  }
  return Guid.Parse(list[0]);
 }
 private Task<List<string>> PartnerRoots(Scope scope,Guid? partner,CancellationToken ct)=>Strings("""
 WITH eligible AS (
 SELECT root_level1_user_id root,partner_level2_user_id partner FROM customer_commercial_owners
 UNION SELECT root_admin_user_id,partner_user_id FROM commercial_orders
 UNION SELECT root_user_id,partner_user_id FROM partner_settlement_batches)
 SELECT DISTINCT e.root::text FROM eligible e
 JOIN identity_users u ON u.id=e.partner
 WHERE e.partner IS NOT NULL AND (@partner::uuid IS NULL OR e.partner=@partner)
 AND (EXISTS(SELECT 1 FROM identity_user_roles r WHERE r.user_id=u.id AND r.role='Level2') OR EXISTS(SELECT 1 FROM partner_settlement_batches b WHERE b.root_user_id=e.root AND b.partner_user_id=e.partner))
 AND ((@isroot AND e.root=@actor) OR (NOT @isroot AND e.partner=@actor))
 """,ct,("partner",partner),("isroot",scope.IsRoot),("actor",scope.Actor));
 public async Task<PartnerSettlementOption[]> GetPartnersAsync(string? bearer,CancellationToken ct=default)
 {
  var s=await Authorize(bearer,false,ct);
  var rows=await Strings("""
 WITH eligible AS (
 SELECT root_level1_user_id root,partner_level2_user_id partner FROM customer_commercial_owners
 UNION SELECT root_admin_user_id,partner_user_id FROM commercial_orders
 UNION SELECT root_user_id,partner_user_id FROM partner_settlement_batches)
 SELECT jsonb_build_object('partnerUserId',u.id,'displayName',COALESCE(NULLIF(trim(p.company_name),''),NULLIF(trim(p.first_name||' '||p.last_name),''),u.normalized_email,u.normalized_mobile,u.id::text),
 'partnerBasisPoints',COALESCE((a.value_json->>'basis_points')::integer,t.partner_basis_points))::text
 FROM (SELECT DISTINCT root,partner FROM eligible WHERE partner IS NOT NULL AND ((@isroot AND root=@actor) OR (NOT @isroot AND partner=@actor))) e
 JOIN identity_users u ON u.id=e.partner LEFT JOIN software_user_profiles p ON p.user_id=u.id
 LEFT JOIN application_settings a ON a.scope_user_id=e.root AND a.setting_key='settlement.partner.'||e.partner::text
 LEFT JOIN LATERAL (SELECT partner_basis_points FROM partner_share_terms WHERE root_user_id=e.root AND partner_user_id=e.partner AND effective_from_utc<=now() AND (effective_until_utc IS NULL OR effective_until_utc>now()) ORDER BY effective_from_utc DESC,id LIMIT 1) t ON TRUE
 WHERE (EXISTS(SELECT 1 FROM identity_user_roles r WHERE r.user_id=u.id AND r.role='Level2') OR EXISTS(SELECT 1 FROM partner_settlement_batches b WHERE b.root_user_id=e.root AND b.partner_user_id=e.partner))
 ORDER BY u.normalized_email,u.id
 """,ct,("isroot",s.IsRoot),("actor",s.Actor));
  return rows.Select(Decode<PartnerSettlementOption>).GroupBy(x=>x.PartnerUserId)
   .Select(g=>g.First() with {PartnerBasisPoints=g.Select(x=>x.PartnerBasisPoints).Distinct().Count()==1?g.First().PartnerBasisPoints:null}).ToArray();
 }
 public async Task<PartnerSettlementOption> SaveTermsAsync(string? bearer,Guid partnerUserId,int partnerBasisPoints,CancellationToken ct=default)
 {
  if(partnerBasisPoints is <0 or >10000)throw new ArgumentOutOfRangeException(nameof(partnerBasisPoints));
  var s=await Authorize(bearer,true,ct);var root=await ResolvePartner(s,partnerUserId,ct);
  await Atomic(async()=>
  {
   await Lock(root,partnerUserId,ct);
   await Execute("""
   INSERT INTO application_settings(scope_user_id,setting_key,value_json,updated_by_user_id,updated_at_utc)
   VALUES(@root,@key,jsonb_build_object('basis_points',@rate),@root,now())
   ON CONFLICT(scope_user_id,setting_key) DO UPDATE SET value_json=EXCLUDED.value_json,updated_by_user_id=EXCLUDED.updated_by_user_id,updated_at_utc=EXCLUDED.updated_at_utc,revision=application_settings.revision+1
   """,ct,("root",root),("key","settlement.partner."+partnerUserId),("rate",partnerBasisPoints));return true;
  },ct);
  return (await GetPartnersAsync(bearer,ct)).Single(x=>x.PartnerUserId==partnerUserId);
 }
 public async Task<PartnerBatchPage> GetBatchesAsync(string? bearer,Guid partnerUserId,int page=1,CancellationToken ct=default)
 {
  var offset=SubscriptionPaging.Offset(page);var s=await Authorize(bearer,false,ct);CheckPartner(partnerUserId);
  if(!s.IsRoot&&s.Actor!=partnerUserId)throw new UnauthorizedAccessException();
  Guid? root=s.IsRoot?await ResolvePartner(s,partnerUserId,ct):null;
  var count=await Strings("SELECT count(*)::text FROM partner_settlement_batches WHERE (@root::uuid IS NULL OR root_user_id=@root) AND partner_user_id=@partner",ct,("root",root),("partner",partnerUserId));
  var items=await Strings("SELECT snapshot::text FROM partner_settlement_batches WHERE (@root::uuid IS NULL OR root_user_id=@root) AND partner_user_id=@partner ORDER BY created_at_utc DESC,id DESC LIMIT 10 OFFSET @offset",ct,("root",root),("partner",partnerUserId),("offset",offset));
  return new(items.Select(Decode<PartnerBatchSummary>).ToArray(),int.Parse(count[0]),page,10);
 }
 public async Task<PartnerBatchDetail> GetBatchAsync(string? bearer,Guid batchId,int page=1,bool print=false,CancellationToken ct=default)
 {
  if(batchId==Guid.Empty)throw new ArgumentException("Invalid batch.");var offset=SubscriptionPaging.Offset(page);var s=await Authorize(bearer,false,ct);
  var headers=await Strings("SELECT snapshot::text FROM partner_settlement_batches WHERE id=@id AND ((@isroot AND root_user_id=@actor) OR (NOT @isroot AND partner_user_id=@actor))",ct,("id",batchId),("isroot",s.IsRoot),("actor",s.Actor));
  if(headers.Count==0)throw new UnauthorizedAccessException();var header=Decode<PartnerBatchSummary>(headers[0]);
  var lines=await Strings("SELECT snapshot::text FROM partner_settlement_batch_lines WHERE batch_id=@id ORDER BY collection_id LIMIT @limit OFFSET @offset",ct,("id",batchId),("limit",print?int.MaxValue:10),("offset",print?0:offset));
  return new(header,lines.Select(Decode<PartnerBatchLine>).ToArray(),header.LineCount,print?1:page,print?header.LineCount:10);
 }
}
