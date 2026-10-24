using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record InvoiceWorkspacePromotionRequest(long ExpectedRevision);
public sealed record InvoiceWorkspacePromotionResult(
    InvoiceApiDecision Decision, Guid? InvoiceId=null, long? SourceRevision=null,
    bool AlreadyPromoted=false, string Code="");

/// <summary>
/// Step19 follow-up: atomically freezes a validated workspace as an INTERNAL
/// InvoiceDocument draft snapshot. It is NOT fiscal issuance / signed payload.
/// Reuses the workspace Guid for idempotent identity; no new DB schema.
/// </summary>
public sealed class InvoiceWorkspacePromotionService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<InvoiceWorkspacePromotionResult> PromoteAsync(
        string? bearer,Guid workspaceId,InvoiceWorkspacePromotionRequest? request,
        CancellationToken ct=default)
    {
        if(workspaceId==Guid.Empty||request?.ExpectedRevision is null or < 1)
            return new(InvoiceApiDecision.Invalid,Code:"invalid_revision");
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);
        var accessible=scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Select(x=>x.Id);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
                await using var tx=await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,ct);
                // Lock source row to serialize preparation against concurrent saves.
                var connection=db.Database.GetDbConnection();
                await using(var lockCommand=connection.CreateCommand()) {
                    lockCommand.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
                    lockCommand.CommandText="SELECT id FROM invoice_entry_workspaces WHERE id=@id FOR UPDATE";
                    var parameter=lockCommand.CreateParameter();
                    parameter.ParameterName="id";parameter.Value=workspaceId;
                    lockCommand.Parameters.Add(parameter);
                    if(await lockCommand.ExecuteScalarAsync(ct) is null)
                        return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.NotFound);
                }
                var draft=await db.InvoiceEntryWorkspaces.AsNoTracking()
                    .Where(w=>w.Id==workspaceId && accessible.Contains(w.TaxpayerId))
                    .SingleOrDefaultAsync(ct);
                if(draft is null)
                    return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.NotFound);
                var currentOwner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
                    .Where(t=>t.Id==draft.TaxpayerId)
                    .Select(t=>t.CustomerUserId).SingleOrDefaultAsync(ct);
                if(currentOwner!=draft.CustomerUserId || currentOwner is null)
                    return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.NotFound);
                if(draft.Revision!=request.ExpectedRevision)
                    return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Conflict,
                        Code:"stale_workspace_revision");
                var existing=await db.InvoiceDocuments.AsNoTracking()
                    .FirstOrDefaultAsync(i=>i.Id==workspaceId &&
                        i.CustomerUserId==draft.CustomerUserId &&
                        i.TaxpayerId==draft.TaxpayerId,ct);
                if(existing is not null)
                {
                    // A lost HTTP response may be retried. Return the same
                    // internal result ONLY if source ID+revision really match.
                    try {
                        using var snapshot=JsonDocument.Parse(existing.SnapshotJson);
                        var root=snapshot.RootElement;
                        if(root.GetProperty("sourceWorkspaceId").GetGuid()==draft.Id &&
                            root.GetProperty("sourceRevision").GetInt64()==draft.Revision)
                            return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Success,
                                existing.Id,draft.Revision,true,
                                "internal_snapshot_already_created");
                    } catch(JsonException) { /* Unrecognized historical payload. */ }
                      catch(KeyNotFoundException) { /* Different existing document. */ }
                    return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Conflict,
                        Code:"workspace_already_bound_to_other_document");
                }
                if(draft.LastCompletedStep!=2)
                    return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Invalid,
                        Code:"incomplete_wizard");
                var input=JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(
                    draft.DraftJson);
                if(input is null||input.TaxpayerId!=draft.TaxpayerId||
                    input.BuyerPartyId is null||
                    input.Lines is not {Length: > 0 and <= 150} ||
                    input.FiscalMemoryId.HasValue == input.SandboxFiscalMemoryId.HasValue ||
                    input.SandboxFiscalMemoryId is null ||
                    input.Type!=1 || input.Pattern is not (1 or 4) ||
                    (input.Pattern==4 && string.IsNullOrWhiteSpace(input.ContractNumber)) ||
                    input.Lines.Any(x=>x.CatalogItemId is null ||
                        x.OfficialIdentifierSnapshot is not {Length:13} ||
                        !x.OfficialIdentifierSnapshot.All(char.IsAsciiDigit) ||
                        x.UnitId is null||x.Quantity<=0||x.UnitPriceRial<0||
                        x.VatRatePercent is null) ||
                    input.SettlementMethod==InvoiceDraftSettlementMethod.Unknown ||
                    input.CashAmountRial is null||input.CreditAmountRial is null ||
                    !InvoiceDraftSettlementPolicy.IsConsistent(input.SettlementMethod,
                        input.CashAmountRial,input.CreditAmountRial,
                        InvoiceDraftSettlementPolicy.Calculate(input.Lines).PreviewGrandTotalRial))
                    return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Invalid,
                        Code:"invoice_not_ready_for_snapshot");


                // Re-resolve each official 13-digit identifier from a trusted
                // active catalogue release at the original issue date. Stored
                // snapshots alone are not legal evidence of a current mapping.
                var itemIds=input.Lines.Select(l=>l.CatalogItemId!.Value)
                    .Distinct().ToArray();
                var mapped=await db.CatalogOfficialMappings.AsNoTracking()
                    .Where(m=>itemIds.Contains(m.ItemId) &&
                        m.EffectiveFromUtc<=draft.IssueDateUtc &&
                        (m.EffectiveToUtc==null || m.EffectiveToUtc>draft.IssueDateUtc))
                    .Join(db.OfficialGoodsServiceEntries.AsNoTracking(),
                        m=>m.OfficialEntryId,e=>e.Id,(m,e)=>new{
                            m.ItemId,e.Code,e.VerificationStatus,e.Kind,e.ReleaseId
                        })
                    .Where(x=>x.VerificationStatus==2 && x.Code.Length==13 &&
                        db.CatalogReferenceReleases.Any(r=>
                            r.Id==x.ReleaseId&&r.State==3))
                    .ToArrayAsync(ct);
                var ownerItems=await db.CustomerCatalogItems.AsNoTracking()
                    .Where(x=>itemIds.Contains(x.Id) && x.CustomerUserId==draft.CustomerUserId &&
                        x.Status==1)
                    .Select(x=>new{x.Id,x.Kind,x.BaseUnitId})
                    .ToDictionaryAsync(x=>x.Id,ct);
                if(ownerItems.Count!=itemIds.Length ||
                    input.Lines.Any(line=>{
                        if(!ownerItems.TryGetValue(line.CatalogItemId!.Value,out var item) ||
                            item.BaseUnitId!=line.UnitId)return true;
                        var candidates=mapped.Where(x=>x.ItemId==item.Id &&
                            (item.Kind is 1 or 2 ? x.Kind==1 : x.Kind==2))
                            .Select(x=>x.Code).Distinct().ToArray();
                        return candidates.Length!=1 ||
                            candidates[0]!=line.OfficialIdentifierSnapshot;
                    }))
                    return new(InvoiceApiDecision.Invalid,
                        Code:"official_item_mapping_changed_or_invalid");
                // Production tax memory must be owned by this seller; Sandbox
                // memory must be an independent, key-ready row of same owner.
                if(input.FiscalMemoryId is { } production){
                    if(!await db.FiscalMemories.AsNoTracking().AnyAsync(m=>
                        m.Id==production&&m.TaxpayerId==draft.TaxpayerId,ct))
                        return new(InvoiceApiDecision.Invalid,Code:"production_memory_not_owned");
                }else{
                    var conn=db.Database.GetDbConnection();
                    await using var cmd=conn.CreateCommand();
                    cmd.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
                    cmd.CommandText="""
SELECT EXISTS(SELECT 1 FROM sandbox_fiscal_memories
WHERE id=@id AND taxpayer_id=@taxpayer AND customer_user_id=@owner
AND private_envelope_json IS NOT NULL)
""";
                    foreach(var (name,value) in new[]{
                        ("id",input.SandboxFiscalMemoryId!.Value),
                        ("taxpayer",draft.TaxpayerId),("owner",draft.CustomerUserId)
                    }){
                        var p=cmd.CreateParameter();p.ParameterName=name;
                        p.Value=value;cmd.Parameters.Add(p);
                    }
                    if(await cmd.ExecuteScalarAsync(ct) is not true)
                        return new(InvoiceApiDecision.Invalid,Code:"sandbox_memory_not_ready");
                }
                var ownerBuyer=await db.CustomerPartyLinks.AsNoTracking()
                    .AnyAsync(x=>x.CustomerUserId==draft.CustomerUserId &&
                        x.PartyId==input.BuyerPartyId && x.UnlinkedAtUtc==null,ct);
                if(!ownerBuyer)
                    return new(InvoiceApiDecision.Invalid,Code:"buyer_not_owned");

                // Stable, server-derived immutable snapshot. This contains no
                // fiscal invoice ID, legal signoff, provider receipt or token.
                var canonical=JsonSerializer.Serialize(new{
                    sourceWorkspaceId=draft.Id,sourceRevision=draft.Revision,
                    environment=input.SandboxFiscalMemoryId.HasValue?"Sandbox":"Production",
                    sandboxFiscalMemoryId=input.SandboxFiscalMemoryId,
                    fiscalMemoryId=input.FiscalMemoryId,
                    invoiceType=input.Type,pattern=input.Pattern,
                    issueDateUtc=draft.IssueDateUtc,
                    buyerPartyId=input.BuyerPartyId,
                    lines=input.Lines,
                    settlementMethod=input.SettlementMethod,
                    cashAmountRial=input.CashAmountRial,
                    creditAmountRial=input.CreditAmountRial,
                    financialSummary=InvoiceDraftSettlementPolicy.Calculate(input.Lines),
                    fiscalSubmissionAuthorized=false,
                    signedPacketAvailable=false
                });
                var hash=Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                    .ToLowerInvariant();
                var now=DateTime.UtcNow;
                db.InvoiceDocuments.Add(new StoredInvoice{
                    Id=draft.Id,CustomerUserId=draft.CustomerUserId,
                    TaxpayerId=draft.TaxpayerId,Revision=1,
                    State=InvoiceLifecycleState.Draft,Subject=InvoiceSubject.Original,
                    IssueDateUtc=draft.IssueDateUtc,SnapshotJson=canonical,
                    SnapshotSha256=hash,UpdatedAtUtc=now
                });
                db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision{
                    InvoiceId=draft.Id,CustomerUserId=draft.CustomerUserId,
                    TaxpayerId=draft.TaxpayerId,Revision=1,SnapshotJson=canonical,
                    SnapshotSha256=hash,CapturedAtUtc=now
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(InvoiceApiDecision.Success,draft.Id,draft.Revision,false,
                    "internal_snapshot_created_not_fiscal_finalized");
            });
        }
        catch(DbUpdateException e) when(e.InnerException is PostgresException pg &&
            pg.SqlState==PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return new(InvoiceApiDecision.Conflict,Code:"duplicate_snapshot_or_history");
        }
        catch(PostgresException pg) when(pg.SqlState is
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return new(InvoiceApiDecision.Conflict,Code:"concurrent_snapshot_conflict");
        }
    }
}
