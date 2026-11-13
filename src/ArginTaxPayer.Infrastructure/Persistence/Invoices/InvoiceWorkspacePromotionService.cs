using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
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
                var isReturnedDraft = false;
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
                    var draftIdentity = JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(draft.DraftJson);
                    isReturnedDraft = existing.State == InvoiceLifecycleState.Draft &&
                        draftIdentity?.BulkImportKey == $"unsent-return:{workspaceId:N}";
                    if (isReturnedDraft)
                    {
                        await using var dispatchCheck = db.Database.GetDbConnection().CreateCommand();
                        dispatchCheck.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
                        dispatchCheck.CommandText = "SELECT EXISTS(SELECT 1 FROM invoice_dispatch_operations WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND invoice_id=@invoice)";
                        foreach (var (name, value) in new[] { ("owner", draft.CustomerUserId), ("taxpayer", draft.TaxpayerId), ("invoice", workspaceId) })
                        { var p = dispatchCheck.CreateParameter(); p.ParameterName = name; p.Value = value; dispatchCheck.Parameters.Add(p); }
                        isReturnedDraft = (bool)(await dispatchCheck.ExecuteScalarAsync(ct) ?? false) == false;
                    }
                    if (!isReturnedDraft)
                        return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Conflict,
                            Code:"workspace_already_bound_to_other_document");
                }
                var input=JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(
                    draft.DraftJson);
                if(input is null || input.TaxpayerId!=draft.TaxpayerId)
                    return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Invalid,
                        Code:"invoice_snapshot_draft_invalid");
                if(input.BuyerPartyId is null)
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_buyer_required");
                if(input.Lines is not {Length: > 0 and <= 150})
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_lines_required");
                if(input.FiscalMemoryId.HasValue == input.SandboxFiscalMemoryId.HasValue)
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_memory_selection_invalid");
                if(input.Type!=1 || input.Pattern is not (1 or 4))
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_pattern_not_supported_for_submission");
                if(input.Pattern==4 && string.IsNullOrWhiteSpace(input.ContractNumber))
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_contract_number_required");
                if(input.Lines.Any(x=>x.CatalogItemId is null ||
                        x.OfficialIdentifierSnapshot is not {Length:13} ||
                        !x.OfficialIdentifierSnapshot.All(char.IsAsciiDigit) || x.UnitId is null || x.Quantity<=0 ||
                        x.UnitPriceRial<0 || x.VatRatePercent is null))
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_line_snapshot_incomplete");
                if(input.SettlementMethod==InvoiceDraftSettlementMethod.Unknown ||
                    input.CashAmountRial is null || input.CreditAmountRial is null)
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_settlement_required");
                if(!InvoiceDraftSettlementPolicy.IsConsistent(input.SettlementMethod,
                        input.CashAmountRial,input.CreditAmountRial,input.Lines,
                        input.OtherTaxAndLegalFeesRial ?? 0m))
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_settlement_mismatch");

                if(draft.IssueDateUtc > DateTime.UtcNow)
                {
                    // RC_IITP.IS V7.9 Table 4 prohibits future issue instants.
                    // This preparation is the point where the draft becomes an
                    // immutable issue candidate. Persist current UTC, advance
                    // the revision, and require fresh review before freezing.
                    var adjustedAt=DateTime.UtcNow;
                    var adjustedInput=input with {
                        Id=null,ExpectedRevision=0,IssueDateUtc=adjustedAt,
                        BulkFingerprint=input.BulkImportKey is null ? null :
                            InvoiceBulkIdentity.Fingerprint(input with {IssueDateUtc=adjustedAt})
                    };
                    var adjustedJson=JsonSerializer.Serialize(adjustedInput);
                    var nextRevision=draft.Revision+1;
                    var updated=await db.InvoiceEntryWorkspaces
                        .Where(w=>w.Id==draft.Id&&w.CustomerUserId==draft.CustomerUserId&&
                            w.TaxpayerId==draft.TaxpayerId&&w.Revision==draft.Revision)
                        .ExecuteUpdateAsync(setters=>setters
                            .SetProperty(w=>w.Revision,nextRevision)
                            .SetProperty(w=>w.IssueDateUtc,adjustedAt)
                            .SetProperty(w=>w.DraftJson,adjustedJson)
                            .SetProperty(w=>w.UpdatedAtUtc,adjustedAt),ct);
                    if(updated!=1)
                        return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Conflict,
                            Code:"stale_workspace_revision");
                    db.InvoiceEntryWorkspaceRevisions.Add(new InvoiceEntryWorkspaceRevision {
                        WorkspaceId=draft.Id,CustomerUserId=draft.CustomerUserId,
                        TaxpayerId=draft.TaxpayerId,Revision=nextRevision,
                        ActorUserId=actor.ActorId,Action="save",
                        Reason="تنظیم زمان آینده در پیش‌نویس اصلاح طبق زمان UTC فعلی",
                        SnapshotJson=adjustedJson,OccurredAtUtc=adjustedAt
                    });
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Conflict,
                        Code:"issue_date_adjusted");
                }


                var itemIds=input.Lines.Select(l=>l.CatalogItemId!.Value)
                    .Distinct().ToArray();
                var ownerItems=await db.CustomerCatalogItems.AsNoTracking()
                    .Where(x=>itemIds.Contains(x.Id) && x.CustomerUserId==draft.CustomerUserId &&
                        x.Status==1)
                    .Select(x=>new{x.Id,x.Kind,x.BaseUnitId})
                    .ToDictionaryAsync(x=>x.Id,ct);
                if(ownerItems.Count!=itemIds.Length ||
                    input.Lines.Any(line=>{
                        if(!ownerItems.TryGetValue(line.CatalogItemId!.Value,out var item) ||
                            item.BaseUnitId!=line.UnitId)return true;
                        return line.OfficialIdentifierSnapshot is not { Length: 13 } code ||
                            !code.All(char.IsAsciiDigit);
                    }))
                    return new(InvoiceApiDecision.Invalid,
                        Code:"official_item_identifier_invalid");
                // Production tax memory must be owned by this seller; Sandbox
                // memory must be an independent, key-ready row of same owner.
                var authorityEnvironment = "Sandbox";
                if(input.FiscalMemoryId is { } modern){
                    var selectedEnvironment = await db.FiscalMemories.AsNoTracking()
                        .Where(m=>m.Id==modern&&m.TaxpayerId==draft.TaxpayerId&&
                            m.Status==FiscalMemoryStatus.Active&&
                            (m.AuthorityEnvironment==FiscalConnectionEnvironment.Production ||
                             m.AuthorityEnvironment==FiscalConnectionEnvironment.Sandbox)&&
                            m.Taxpayer.CustomerUserId==draft.CustomerUserId&&
                            db.CustomerTaxpayerMemberships.Any(link=>
                                link.CustomerUserId==draft.CustomerUserId&&
                                link.TaxpayerId==draft.TaxpayerId&&link.UnlinkedAtUtc==null))
                        .Select(m=>m.AuthorityEnvironment).SingleOrDefaultAsync(ct);
                    if(selectedEnvironment is null)
                        return new(InvoiceApiDecision.Invalid,Code:"fiscal_memory_not_owned_active_or_classified");
                    authorityEnvironment = selectedEnvironment.Value.ToString();
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
                var buyerPersonType=await (from link in db.CustomerPartyLinks.AsNoTracking()
                    join party in db.Parties.AsNoTracking() on link.PartyId equals party.Id
                    where link.CustomerUserId==draft.CustomerUserId &&
                        link.PartyId==input.BuyerPartyId && link.UnlinkedAtUtc==null &&
                        party.Status==PartyStatus.Active
                    select (int?)party.Kind).SingleOrDefaultAsync(ct);
                if(buyerPersonType is null)
                    return new(InvoiceApiDecision.Invalid,Code:"buyer_not_owned");

                InvoiceFinancialPreview financialPreview;
                try
                {
                    financialPreview=InvoiceFinancialCalculator.Preview(
                        input.Lines.Select((line,index)=>new InvoiceFinancialInput(
                            line.CatalogItemId!.Value,line.Quantity,line.UnitPriceRial,
                            decimal.Truncate(line.Quantity*line.DiscountPerUnitRial),
                            line.VatRatePercent, OtherTaxesRial:
                                index==0 ? input.OtherTaxAndLegalFeesRial ?? 0m : 0m)).ToArray(),
                        new InvoiceTypePattern(ElectronicInvoiceType.First,1),
                        new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition));
                }
                catch(ArgumentException)
                {
                    return new(InvoiceApiDecision.Invalid,Code:"invoice_financial_calculation_invalid");
                }

                // Stable, server-derived immutable snapshot. This contains no
                // fiscal invoice ID, legal signoff, provider receipt or token.
                var canonical=JsonSerializer.Serialize(new{
                    sourceWorkspaceId=draft.Id,sourceRevision=draft.Revision,
                    environment=authorityEnvironment,
                    sandboxFiscalMemoryId=input.SandboxFiscalMemoryId,
                    fiscalMemoryId=input.FiscalMemoryId,
                    invoiceType=input.Type,pattern=input.Pattern,
                    issueDateUtc=draft.IssueDateUtc,
                    buyerPartyId=input.BuyerPartyId,
                    buyerPersonType=buyerPersonType.Value,
                    lines=input.Lines,
                    settlementMethod=input.SettlementMethod,
                    cashAmountRial=input.CashAmountRial,
                    creditAmountRial=input.CreditAmountRial,
                    financialSummary=new InvoiceDraftFinancialSummary(
                        financialPreview.BeforeDiscountRial,financialPreview.DiscountRial,
                        financialPreview.AfterDiscountRial,financialPreview.VatRial??0m,
                        financialPreview.GrandTotalRial??0m),
                    otherTaxAndLegalFeesRial=input.OtherTaxAndLegalFeesRial ?? 0m,
                    fiscalSubmissionAuthorized=false,
                    signedPacketAvailable=false
                });
                var hash=Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                    .ToLowerInvariant();
                var now=DateTime.UtcNow;
                var nextDocumentRevision = isReturnedDraft ? existing!.Revision + 1 : 1;
                if (isReturnedDraft)
                {
                    var changed = await db.InvoiceDocuments.Where(i => i.Id == draft.Id &&
                        i.CustomerUserId == draft.CustomerUserId && i.TaxpayerId == draft.TaxpayerId &&
                        i.Revision == existing!.Revision && i.State == InvoiceLifecycleState.Draft)
                        .ExecuteUpdateAsync(set => set.SetProperty(i => i.Revision, nextDocumentRevision)
                            .SetProperty(i => i.SnapshotJson, canonical).SetProperty(i => i.SnapshotSha256, hash)
                            .SetProperty(i => i.IssueDateUtc, draft.IssueDateUtc).SetProperty(i => i.UpdatedAtUtc, now), ct);
                    if (changed != 1)
                        return new InvoiceWorkspacePromotionResult(InvoiceApiDecision.Conflict,
                            Code:"concurrent_invoice_change");
                }
                else
                    db.InvoiceDocuments.Add(new StoredInvoice{
                        Id=draft.Id,CustomerUserId=draft.CustomerUserId,
                        TaxpayerId=draft.TaxpayerId,Revision=nextDocumentRevision,
                        State=InvoiceLifecycleState.Draft,Subject=InvoiceSubject.Original,
                        IssueDateUtc=draft.IssueDateUtc,SnapshotJson=canonical,
                        SnapshotSha256=hash,UpdatedAtUtc=now
                    });
                db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision{
                    InvoiceId=draft.Id,CustomerUserId=draft.CustomerUserId,
                    TaxpayerId=draft.TaxpayerId,Revision=nextDocumentRevision,SnapshotJson=canonical,
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
