using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Step22 read-only, server-owned operational blockers. This is NOT a fiscal
/// authorization endpoint and cannot generate fiscal identifiers or signatures.
/// </summary>
public sealed record Phase10LiveSendReadiness(
    string Environment, bool ReadyForLiveSend, bool SubmissionEnabled,
    bool SignedPacketAvailable, bool FiscalAccepted, bool HasImmutableSnapshot,
    string InvoiceState, string[] BlockingCodes);
public sealed record Phase10LiveSendReadinessResult(
    InvoiceApiDecision Decision, Phase10LiveSendReadiness? Readiness=null);

public sealed class Phase10LiveSendReadinessService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<Phase10LiveSendReadinessResult> ReadAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId,
        CancellationToken ct=default)
    {
        if(taxpayerId==Guid.Empty || invoiceId==Guid.Empty)
            return new(InvoiceApiDecision.Invalid);
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if(owner is null || owner==Guid.Empty)
            return new(InvoiceApiDecision.NotFound);

        var invoice=await db.InvoiceDocuments.AsNoTracking().Where(i=>
            i.Id==invoiceId && i.CustomerUserId==owner.Value &&
            i.TaxpayerId==taxpayerId)
            .Select(i=>new{i.State,i.SnapshotJson}).SingleOrDefaultAsync(ct);
        if(invoice is null)return new(InvoiceApiDecision.NotFound);

        var blockers=new List<string>();
        var environment="Unverified";
        var hasInternalSnapshot=false;
        try {
            using var document=JsonDocument.Parse(invoice.SnapshotJson);
            var root=document.RootElement;
            // Never trust user-supplied or stale JSON readiness booleans.
            if(root.TryGetProperty("sourceWorkspaceId",out var src) &&
                src.ValueKind==JsonValueKind.String &&
                src.TryGetGuid(out var sourceId) && sourceId==invoiceId &&
                root.TryGetProperty("sourceRevision",out var rev) &&
                rev.ValueKind==JsonValueKind.Number && rev.GetInt64()>0)
                hasInternalSnapshot=true;
            if(hasInternalSnapshot &&
                root.TryGetProperty("environment",out var env) &&
                env.ValueKind==JsonValueKind.String && env.GetString()=="Sandbox" &&
                root.TryGetProperty("sandboxFiscalMemoryId",out var mem) &&
                mem.ValueKind==JsonValueKind.String && mem.TryGetGuid(out var keyId) &&
                keyId!=Guid.Empty)
                environment="Sandbox";
        } catch(JsonException) {
            blockers.Add("invoice.snapshot.invalid");
        } catch(FormatException) {
            blockers.Add("invoice.snapshot.invalid");
        }
        if(!hasInternalSnapshot)
            blockers.Add("invoice.snapshot.not_internal_promoted");
        if(environment!="Sandbox")
            blockers.Add("invoice.sandbox.memory_not_bound");
        if(invoice.State!=InvoiceLifecycleState.Finalized)
            blockers.Add("invoice.finalization.not_finalized");
        // No signed provider packet, fiscal identifier, authoritative
        // rules evidence or enabled Sandbox transport are wired to this flow.
        // Do not turn these off based on fields supplied by a client.
        blockers.Add("invoice.rules.not_submission_verified");
        blockers.Add("invoice.fiscal_identifier.not_issued");
        blockers.Add("invoice.signed_packet.not_prepared");
        blockers.Add("invoice.sandbox_outbox.not_connected");
        blockers.Add("invoice.transport.disabled");
        blockers.Add("invoice.owner_live_send_approval.required");
        return new(InvoiceApiDecision.Success,new(
            environment,false,false,false,false,hasInternalSnapshot,
            invoice.State.ToString(),blockers.Distinct().ToArray()));
    }
}
