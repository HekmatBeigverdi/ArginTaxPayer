using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Step22 read-only, server-owned operational blockers. This is NOT a fiscal
/// authorization endpoint and cannot generate fiscal identifiers or signatures.
/// </summary>
public sealed record Phase10LiveSendReadiness(
    string Environment, bool ReadyForLiveSend, bool SubmissionEnabled,
    bool SignedPacketAvailable, bool FiscalAccepted, bool HasImmutableSnapshot,
    string InvoiceState, long InvoiceRevision, Guid? SandboxFiscalMemoryId,
    string[] BlockingCodes);
public sealed record Phase10LiveSendReadinessResult(
    InvoiceApiDecision Decision, Phase10LiveSendReadiness? Readiness=null);

public sealed class Phase10LiveSendReadinessService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope, IConfiguration configuration)
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
            .Select(i=>new{i.State,i.Revision,i.SnapshotJson}).SingleOrDefaultAsync(ct);
        if(invoice is null)return new(InvoiceApiDecision.NotFound);

        var reviewState=await db.InvoiceReviews.AsNoTracking()
            .Where(r=>r.InvoiceId==invoiceId && r.CustomerUserId==owner.Value &&
                r.TaxpayerId==taxpayerId)
            .Select(r=>(InvoiceReviewState?)r.State).SingleOrDefaultAsync(ct)
            ?? InvoiceReviewState.Draft;
        var blockers=new List<string>();
        var environment="Unverified";
        var hasInternalSnapshot=false;
        Guid? sandboxFiscalMemoryId=null;
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
            {
                environment="Sandbox";
                sandboxFiscalMemoryId=keyId;
            }
        } catch(JsonException) {
            blockers.Add("invoice.snapshot.invalid");
        } catch(FormatException) {
            blockers.Add("invoice.snapshot.invalid");
        }
        if(!hasInternalSnapshot)
            blockers.Add("invoice.snapshot.not_internal_promoted");
        if(environment!="Sandbox")
            blockers.Add("invoice.sandbox.memory_not_bound");
        if(invoice.State==InvoiceLifecycleState.Draft)
            blockers.Add("invoice.lifecycle.validation_required");
        if(reviewState!=InvoiceReviewState.Approved)
            blockers.Add("invoice.review.approval_required");
        if(invoice.State!=InvoiceLifecycleState.Finalized)
            blockers.Add("invoice.finalization.not_finalized");
        // These are server-owned evidence gates. Do not turn them off based on
        // fields supplied by a client. The selected wire profile controls only
        // which protocol-specific evidence is relevant.
        blockers.Add("invoice.rules.not_submission_verified");
        // A fiscal ID must be grounded in the durable reservation row.
        // Never infer issuance from a user-supplied snapshot field.
        var reserved = false;
        if (invoice.State == InvoiceLifecycleState.Finalized &&
            sandboxFiscalMemoryId is { } memoryId)
        {
            var connection = db.Database.GetDbConnection();
            var opened = connection.State != System.Data.ConnectionState.Open;
            if (opened) await connection.OpenAsync(ct);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
SELECT EXISTS (
 SELECT 1 FROM sandbox_invoice_fiscal_reservations
 WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND
  invoice_id=@invoice AND invoice_revision=@revision AND
  sandbox_memory_id=@memory AND fiscal_tax_id IS NOT NULL
)
""";
                foreach (var (name,value) in new (string,object)[] {
                    ("owner",owner.Value),("taxpayer",taxpayerId),
                    ("invoice",invoiceId),("revision",invoice.Revision),
                    ("memory",memoryId)
                })
                {
                    var parameter=command.CreateParameter();
                    parameter.ParameterName=name;
                    parameter.Value=value;
                    command.Parameters.Add(parameter);
                }
                reserved = (bool)(await command.ExecuteScalarAsync(ct) ?? false);
            }
            finally
            {
                if (opened) await connection.CloseAsync();
            }
        }
        if (!reserved)
        {
            blockers.Add("invoice.fiscal_identifier.not_issued");
            blockers.Add("invoice.serial.durable_reservation_required");
        }
        var profile = configuration["Phase10Submission:Profile"]?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(profile)) profile = "legacy";
        if (profile == "legacy")
        {
            if (configuration["SandboxInvoiceLegacy:Enabled"] != "true" ||
                configuration["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] != "true")
                blockers.Add("invoice.legacy.transport_not_registered");
        }
        else
        {
            blockers.Add("invoice.sandbox.certificate_chain_not_verified");
            blockers.Add("invoice.v2.protocol_not_verified_for_sandbox");
        }
        // These are reported until durable operation evidence exists. The
        // transport and operator gates are configuration-backed and must not
        // remain visible after explicit local Sandbox approval.
        blockers.Add("invoice.signed_packet.not_prepared");
        blockers.Add("invoice.sandbox_outbox.not_connected");
        if (configuration["SandboxInvoiceLegacy:Enabled"] != "true" ||
            configuration["SandboxInvoiceLegacy:OperatorApprovedSend"] != "true")
            blockers.Add("invoice.transport.disabled");
        if (configuration["SandboxInvoiceLegacy:OperatorApprovedSend"] != "true")
            blockers.Add("invoice.owner_live_send_approval.required");
        return new(InvoiceApiDecision.Success,new(
            environment,false,false,false,false,hasInternalSnapshot,
            invoice.State.ToString(),invoice.Revision,sandboxFiscalMemoryId,
            blockers.Distinct().ToArray()));
    }
}
