using System.Data;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record LegacyCorrectionDraftResult(InvoiceApiDecision Decision,string SafeCode,
    Guid? WorkspaceId=null,bool IssueDateAdjusted=false);

/// <summary>Creates one linked editable draft after a fresh, correlated FAILED inquiry.
/// Never alters the finalized invoice, its tax ID, signed payload or dispatch state.</summary>
public sealed class LegacyInvoiceCorrectionDraftService(ArginTaxPayerDbContext db,
    TaxpayerScopeService scope,Phase18InvoiceInquiryService inquiry,
    Phase10InvoiceDispatchReadService dispatch,
    Phase18ProductionAuthorityReportRepository productionReports,
    LegacyAuthorityReportRepository sandboxReports,
    InvoiceEntryWorkspaceService workspaces)
{
    public async Task<LegacyCorrectionDraftResult> CreateAsync(string? bearer,Guid taxpayerId,
        Guid invoiceId,CancellationToken ct)
    {
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated,"unauthenticated");
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden,"forbidden");
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1).Where(t=>t.Id==taxpayerId)
            .Select(t=>t.CustomerUserId).SingleOrDefaultAsync(ct);
        if(owner is null)return new(InvoiceApiDecision.NotFound,"not_found");
        var invoice=await db.InvoiceDocuments.AsNoTracking().SingleOrDefaultAsync(i=>
            i.Id==invoiceId&&i.CustomerUserId==owner.Value&&i.TaxpayerId==taxpayerId,ct);
        if(invoice is null)return new(InvoiceApiDecision.NotFound,"not_found");
        if(await RejectedInvoiceReplacementQuery.ResolveAsync(db,owner.Value,taxpayerId,invoiceId,ct)!=invoiceId)
            return new(InvoiceApiDecision.Conflict,"rejected_invoice_already_replaced");

        var previous=await FindLinkedAsync(owner.Value,taxpayerId,invoiceId,ct);
        if(previous is not null)
        {
            var repaired=await SetCurrentDateIfFutureAsync(bearer,owner.Value,taxpayerId,previous.Value,ct);
            return repaired.Decision==InvoiceApiDecision.Success
                ? new(InvoiceApiDecision.Success,"correction_draft_exists",previous,repaired.Adjusted)
                : new(repaired.Decision,repaired.Code);
        }
        var fresh=await inquiry.InquireAsync(bearer,taxpayerId,invoiceId,ct);
        if(fresh.Decision!=InvoiceApiDecision.Success)
            return new(fresh.Decision,fresh.SafeCode);
        if(fresh.AuthorityResult?.CanPrepareCorrection!=true)
            return new(InvoiceApiDecision.Conflict,"authority_rejection_required");
        if(fresh.Environment is not ("Production" or "Sandbox"))
            return new(InvoiceApiDecision.Conflict,"authority_environment_unavailable");
        var production=fresh.Environment=="Production";
        Guid operationId;
        Guid? sandboxReportId=null;
        if(production)
        {
            var current=await dispatch.GetAsync(bearer,taxpayerId,invoiceId,ct);
            if(current.Decision!=InvoiceApiDecision.Success || current.Overview is not
                {Status:"Acknowledged",Environment:"Production",ReferenceNumber:not null} sent ||
                sent.ReferenceNumber!=fresh.ReferenceNumber)
                return new(InvoiceApiDecision.Conflict,"authority_rejection_required");
            operationId=sent.OperationId;
            var stored=await productionReports.ReadAsync(owner.Value,taxpayerId,invoiceId,
                operationId,sent.ReferenceNumber,ct);
            if(stored?.CanPrepareCorrection!=true)
                return new(InvoiceApiDecision.Conflict,"authority_rejection_required");
        }
        else
        {
            // Preserve the legacy Sandbox flow and its report-to-correction link.
            var stored=await sandboxReports.ReadAsync(owner.Value,taxpayerId,invoiceId,ct);
            if(stored is null || !stored.Report.CanPrepareCorrection ||
                stored.ReferenceNumber!=fresh.ReferenceNumber)
                return new(InvoiceApiDecision.Conflict,"authority_rejection_required");
            operationId=stored.OperationId;
            sandboxReportId=stored.Id;
        }

        using var snapshot=JsonDocument.Parse(invoice.SnapshotJson);
        if (!snapshot.RootElement.TryGetProperty("sourceWorkspaceId", out var sourceElement) ||
            !sourceElement.TryGetGuid(out var sourceId) ||
            !snapshot.RootElement.TryGetProperty("sourceRevision", out var revisionElement) ||
            !revisionElement.TryGetInt64(out var sourceRevision))
            return new(InvoiceApiDecision.Invalid,"source_workspace_missing");
        var source=await db.InvoiceEntryWorkspaces.AsNoTracking().SingleOrDefaultAsync(w=>
            w.Id==sourceId&&w.CustomerUserId==owner.Value&&w.TaxpayerId==taxpayerId,ct);
        if(source is null)return new(InvoiceApiDecision.Invalid,"source_workspace_missing");
        if(source.Revision!=sourceRevision)
            return new(InvoiceApiDecision.Conflict,"source_workspace_revision_mismatch");
        var input=JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(source.DraftJson);
        if(input is null || (production
            ? input.FiscalMemoryId is null || input.SandboxFiscalMemoryId is not null
            : input.SandboxFiscalMemoryId is null || input.FiscalMemoryId is not null))
            return new(InvoiceApiDecision.Invalid,production?"production_snapshot_required":"sandbox_snapshot_required");
        var issueDateAdjusted=input.IssueDateUtc>DateTime.UtcNow;
        if(issueDateAdjusted)input=input with {IssueDateUtc=DateTime.UtcNow};
        var keyPrefix=(production?"production-resubmission:":"sandbox-correction:")+operationId.ToString("N");
        var key=keyPrefix;
        var stableId=InvoiceBulkIdentity.StableWorkspaceId(owner.Value,key);
        // A workspace may already have been frozen as an internal invoice snapshot.
        // Never reopen that immutable workspace for editing: fork its latest draft
        // contents into the next deterministic workspace for this rejected dispatch.
        if(production)
        {
            SaveInvoiceEntryRequest latestDraft=input;
            var foundEditableOrMissing=false;
            for(var generation=0;generation<100;generation++)
            {
                key=generation==0?keyPrefix:$"{keyPrefix}:retry:{generation}";
                stableId=InvoiceBulkIdentity.StableWorkspaceId(owner.Value,key);
                var priorWorkspace=await db.InvoiceEntryWorkspaces.AsNoTracking()
                    .SingleOrDefaultAsync(w=>w.Id==stableId&&w.CustomerUserId==owner.Value&&
                        w.TaxpayerId==taxpayerId,ct);
                if(priorWorkspace is null){foundEditableOrMissing=true;break;}
                var isFrozen=await db.InvoiceDocuments.AsNoTracking().AnyAsync(d=>
                    d.Id==stableId&&d.CustomerUserId==owner.Value&&d.TaxpayerId==taxpayerId,ct);
                try
                {
                    latestDraft=JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(priorWorkspace.DraftJson)
                        ?? latestDraft;
                }
                catch(JsonException) { /* Keep the last valid draft as the copy source. */ }
                if(!isFrozen){foundEditableOrMissing=true;break;} // Reuse this editable retry.
            }
            if(!foundEditableOrMissing)
                return new(InvoiceApiDecision.Conflict,"correction_draft_generations_exhausted");
            input=latestDraft;
            issueDateAdjusted|=input.IssueDateUtc>DateTime.UtcNow;
            if(issueDateAdjusted)input=input with {IssueDateUtc=DateTime.UtcNow};
        }
        // Recover an already-created editable draft after an interrupted response,
        // including user edits. A frozen workspace is skipped by the production loop above.
        var saved=await workspaces.GetAsync(bearer,stableId,ct);
        if(saved.Decision!=InvoiceApiDecision.Success)
            saved=await workspaces.SaveAsync(bearer,input with {
                Id=null,TaxpayerId=taxpayerId,ExpectedRevision=0,
                BulkImportKey=key,BulkFingerprint=null,
                InternalReviewState="Draft",InternalReviewReason=null,
                SubmittedByUserId=null,ReviewedByUserId=null,ReviewedAtUtc=null
            },ct);
        if(saved.Decision!=InvoiceApiDecision.Success)
            return new(saved.Decision,$"correction_draft_{saved.Decision.ToString().ToLowerInvariant()}");
        if(saved.Draft is null)
            return new(InvoiceApiDecision.Invalid,"correction_draft_empty");
        var normalized=await SetCurrentDateIfFutureAsync(bearer,owner.Value,taxpayerId,
            saved.Draft.Id,ct);
        if(normalized.Decision!=InvoiceApiDecision.Success)
            return new(normalized.Decision,normalized.Code);
        issueDateAdjusted|=normalized.Adjusted;

        if(production)
        {
            if(invoice.Subject==InvoiceSubject.Original &&
                !await RejectedInvoiceReplacementQuery.RegisterAsync(db,owner.Value,taxpayerId,
                    invoiceId,operationId,saved.Draft.Id,ct))
                return new(InvoiceApiDecision.Conflict,"replacement_registration_failed");
            return new(InvoiceApiDecision.Success,"correction_draft_created",saved.Draft.Id,issueDateAdjusted);
        }

        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            using var command=connection.CreateCommand();
            command.CommandText="""
INSERT INTO sandbox_invoice_correction_drafts
(operation_id,report_id,customer_user_id,taxpayer_id,invoice_id,workspace_id,actor_user_id,created_at_utc)
SELECT @operation,@report,@owner,@taxpayer,@invoice,@workspace,@actor,@now
WHERE EXISTS(SELECT 1 FROM sandbox_authority_inquiry_reports r
 WHERE r.id=@report AND r.operation_id=@operation AND r.customer_user_id=@owner
 AND r.taxpayer_id=@taxpayer AND r.invoice_id=@invoice
 AND r.report_json->>'CanPrepareCorrection'='true')
ON CONFLICT(operation_id) DO NOTHING
""";
            foreach(var (name,value) in new (string,object)[]{
                ("operation",operationId),("report",sandboxReportId!.Value),("owner",owner.Value),
                ("taxpayer",taxpayerId),("invoice",invoiceId),("workspace",saved.Draft.Id),
                ("actor",actor.ActorId),("now",DateTime.UtcNow)})
                LegacyAuthorityReportRepository.Add(command,name,value);
            await command.ExecuteNonQueryAsync(ct);
        }
        finally {if(opened)await connection.CloseAsync();}
        var linked=await FindLinkedAsync(owner.Value,taxpayerId,invoiceId,ct);
        return linked is null ? new(InvoiceApiDecision.Conflict,"correction_link_conflict") :
            new(InvoiceApiDecision.Success,"correction_draft_created",linked,issueDateAdjusted);
    }

    private async Task<(InvoiceApiDecision Decision,string Code,bool Adjusted)>
        SetCurrentDateIfFutureAsync(string? bearer,Guid owner,Guid taxpayer,Guid workspaceId,
            CancellationToken ct)
    {
        var row=await db.InvoiceEntryWorkspaces.AsNoTracking().SingleOrDefaultAsync(w=>
            w.Id==workspaceId&&w.CustomerUserId==owner&&w.TaxpayerId==taxpayer,ct);
        if(row is null)return (InvoiceApiDecision.NotFound,"correction_workspace_missing",false);
        if(row.IssueDateUtc<=DateTime.UtcNow)
            return (InvoiceApiDecision.Success,"correction_draft_exists",false);
        SaveInvoiceEntryRequest? input;
        try {input=JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(row.DraftJson);}
        catch(JsonException) {return (InvoiceApiDecision.Invalid,"correction_workspace_invalid",false);}
        if(input is null)return (InvoiceApiDecision.Invalid,"correction_workspace_invalid",false);
        var saved=await workspaces.SaveAsync(bearer,input with {
            Id=row.Id,ExpectedRevision=row.Revision,IssueDateUtc=DateTime.UtcNow
        },ct);
        return saved.Decision==InvoiceApiDecision.Success&&saved.Draft is not null
            ? (InvoiceApiDecision.Success,"correction_date_reset",true)
            : (saved.Decision,$"correction_draft_{saved.Decision.ToString().ToLowerInvariant()}",false);
    }

    private async Task<Guid?> FindLinkedAsync(Guid owner,Guid taxpayer,Guid invoice,CancellationToken ct)
    {
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            using var command=connection.CreateCommand();
            command.CommandText="""
SELECT c.workspace_id FROM sandbox_invoice_correction_drafts c
JOIN invoice_entry_workspaces w ON w.id=c.workspace_id AND w.customer_user_id=c.customer_user_id
 AND w.taxpayer_id=c.taxpayer_id
WHERE c.customer_user_id=@owner AND c.taxpayer_id=@taxpayer AND c.invoice_id=@invoice
ORDER BY c.created_at_utc DESC LIMIT 1
""";
            LegacyAuthorityReportRepository.Add(command,"owner",owner);
            LegacyAuthorityReportRepository.Add(command,"taxpayer",taxpayer);
            LegacyAuthorityReportRepository.Add(command,"invoice",invoice);
            return await command.ExecuteScalarAsync(ct) is Guid id?id:null;
        }
        finally {if(opened)await connection.CloseAsync();}
    }

}
