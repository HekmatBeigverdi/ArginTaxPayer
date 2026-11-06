using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Signing.Connection;
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
    string[] BlockingCodes, Guid? FiscalMemoryId = null);
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
            .Select(i=>new{i.State,i.Revision,i.SnapshotJson,i.SnapshotSha256}).SingleOrDefaultAsync(ct);
        if(invoice is null)return new(InvoiceApiDecision.NotFound);

        var reviewState=await db.InvoiceReviews.AsNoTracking()
            .Where(r=>r.InvoiceId==invoiceId && r.CustomerUserId==owner.Value &&
                r.TaxpayerId==taxpayerId)
            .Select(r=>(InvoiceReviewState?)r.State).SingleOrDefaultAsync(ct)
            ?? InvoiceReviewState.Draft;
        if (Phase18InvoiceMemoryBindingService.TryReadMemory(invoice.SnapshotJson,
                out _, out var legacySandbox, out var selectedEnvironment) && !legacySandbox)
        {
            var binding = await new Phase18InvoiceMemoryBindingService(db, scope,
                new Phase18PersistedAuthorityEnvironmentResolver(db)).ResolveAsync(bearer, taxpayerId, invoiceId, ct);
            return await ReadScopedAsync(binding, selectedEnvironment, taxpayerId, invoiceId, invoice.State,
                invoice.Revision, invoice.SnapshotJson, invoice.SnapshotSha256, reviewState, ct);
        }
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
        // Legacy rules become evidenced only after the strict Type1/Pattern1
        // server-side projection and durable signed Outbox preparation.
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
        // A signed packet and a durable Outbox message are proven by an
        // identical operation ID, trusted scope, revision, memory and hash.
        // Never trust the frozen invoice's old signedPacketAvailable flag.
        var signedPacketAvailable=false;
        if (invoice.State==InvoiceLifecycleState.Finalized &&
            sandboxFiscalMemoryId is { } boundMemory)
        {
            var conn=db.Database.GetDbConnection();
            var opened=conn.State!=System.Data.ConnectionState.Open;
            if(opened)await conn.OpenAsync(ct);
            try
            {
                await using var cmd=conn.CreateCommand();
                cmd.CommandText="""
SELECT EXISTS(
 SELECT 1 FROM invoice_dispatch_operations d
 JOIN outbox_messages o ON o.id=d.id AND o.taxpayer_id=d.taxpayer_id
 WHERE d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer
  AND d.invoice_id=@invoice AND d.invoice_revision=@revision
  AND d.fiscal_memory_id=@memory
  AND o.event_type='invoice.sandbox.legacy.dispatch.requested'
  AND o.payload_json::jsonb->>'environment'='Sandbox'
  AND o.payload_json::jsonb->>'requestSha256'=d.payload_sha256
  AND o.payload_json::jsonb->>'invoiceId'=@invoice_text
  AND o.payload_json::jsonb->>'fiscalMemoryId'=@memory_text
)
""";
                foreach(var (name,value) in new (string,object)[] {
                    ("owner",owner.Value),("taxpayer",taxpayerId),
                    ("invoice",invoiceId),("revision",invoice.Revision),
                    ("memory",boundMemory),
                    ("invoice_text",invoiceId.ToString("D")),
                    ("memory_text",boundMemory.ToString("D"))
                })
                {
                    var p=cmd.CreateParameter();
                    p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);
                }
                signedPacketAvailable=(bool)(await cmd.ExecuteScalarAsync(ct)??false);
            }
            finally { if(opened)await conn.CloseAsync(); }
        }
        if(!signedPacketAvailable)
        {
            blockers.Add("invoice.rules.not_submission_verified");
            blockers.Add("invoice.signed_packet.not_prepared");
            blockers.Add("invoice.sandbox_outbox.not_connected");
        }
        if (configuration["SandboxInvoiceLegacy:Enabled"] != "true" ||
            configuration["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] != "true")
            blockers.Add("invoice.transport.disabled");
        var ready = environment == "Sandbox" && signedPacketAvailable &&
            blockers.Count == 0;
        return new(InvoiceApiDecision.Success,new(
            environment,ready,ready,signedPacketAvailable,false,hasInternalSnapshot,
            invoice.State.ToString(),invoice.Revision,sandboxFiscalMemoryId,
            blockers.Distinct().ToArray()));
    }
    private async Task<Phase10LiveSendReadinessResult> ReadScopedAsync(
        Phase18InvoiceMemoryBindingResult binding, TaxAuthorityEnvironment selectedEnvironment, Guid taxpayerId, Guid invoiceId,
        InvoiceLifecycleState state, long revision, string snapshot, string snapshotHash,
        InvoiceReviewState reviewState, CancellationToken ct)
    {
        var blockers = new List<string>();
        var verified = binding.Decision == InvoiceApiDecision.Success &&
            binding.Environment is TaxAuthorityEnvironment.Production or TaxAuthorityEnvironment.Sandbox;
        var environment = verified ? binding.Environment.ToString() : "Unverified";
        var provider = verified ? binding.Environment : selectedEnvironment;
        var providerName = provider == TaxAuthorityEnvironment.Production ? "production" : "sandbox";
        if (!verified) blockers.Add($"invoice.{providerName}.memory_not_bound");
        var hasSnapshot = Phase10InternalInvoiceValidationService.ValidSnapshot(
            snapshot, snapshotHash, invoiceId);
        if (!hasSnapshot) blockers.Add("invoice.snapshot.not_internal_promoted");
        if (state == InvoiceLifecycleState.Draft) blockers.Add("invoice.lifecycle.validation_required");
        if (reviewState != InvoiceReviewState.Approved) blockers.Add("invoice.review.approval_required");
        if (state != InvoiceLifecycleState.Finalized) blockers.Add("invoice.finalization.not_finalized");
        var reserved = false;
        var signed = false;
        if (verified && state == InvoiceLifecycleState.Finalized)
        {
            var conn = db.Database.GetDbConnection();
            var opened = conn.State != System.Data.ConnectionState.Open;
            if (opened) await conn.OpenAsync(ct);
            try
            {
                await using var command = conn.CreateCommand();
                command.CommandText = """
SELECT EXISTS(SELECT 1 FROM authority_invoice_fiscal_reservations r
 WHERE r.customer_user_id=@owner AND r.taxpayer_id=@taxpayer AND r.invoice_id=@invoice
 AND r.invoice_revision=@revision AND r.fiscal_memory_id=@memory AND r.fiscal_tax_id IS NOT NULL
 AND r.authority_environment=@authority_environment),
 EXISTS(SELECT 1 FROM invoice_dispatch_operations d
 JOIN outbox_messages o ON o.id=d.id AND o.taxpayer_id=d.taxpayer_id
 WHERE d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer AND d.invoice_id=@invoice
 AND d.invoice_revision=@revision AND d.fiscal_memory_id=@memory AND d.authority_environment=@authority_environment
 AND o.event_type=@event
 AND o.payload_json::jsonb->>'environment'=@environment
 AND o.payload_json::jsonb->>'requestSha256'=d.payload_sha256
 AND o.payload_json::jsonb->>'invoiceId'=@invoice_text
 AND o.payload_json::jsonb->>'fiscalMemoryId'=@memory_text)
""";
                foreach (var (name, value) in new (string, object)[] {
                    ("owner", binding.Owner), ("taxpayer", taxpayerId), ("invoice", invoiceId),
                    ("revision", revision), ("memory", binding.MemoryId),
                    ("authority_environment", provider == TaxAuthorityEnvironment.Production ? 1 : 2),
                    ("event", $"invoice.{providerName}.legacy.dispatch.requested"), ("environment", environment),
                    ("invoice_text", invoiceId.ToString("D")), ("memory_text", binding.MemoryId.ToString("D")) })
                {
                    var p = command.CreateParameter(); p.ParameterName = name; p.Value = value;
                    command.Parameters.Add(p);
                }
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct)) { reserved = reader.GetBoolean(0); signed = reader.GetBoolean(1); }
            }
            finally { if (opened) await conn.CloseAsync(); }
        }
        if (!reserved)
        {
            blockers.Add("invoice.fiscal_identifier.not_issued");
            blockers.Add("invoice.serial.durable_reservation_required");
        }
        if (!signed)
        {
            blockers.Add("invoice.rules.not_submission_verified");
            blockers.Add("invoice.signed_packet.not_prepared");
            blockers.Add($"invoice.{providerName}_outbox.not_connected");
        }
        if (!Phase18LegacyAuthorityEndpointSelector.TryResolveConfiguredBase(
                provider, configuration, out _))
        {
            blockers.Add(provider == TaxAuthorityEnvironment.Production
                ? "invoice.production.transport_not_registered" : "invoice.legacy.transport_not_registered");
            blockers.Add("invoice.transport.disabled");
        }
        var ready = verified && signed && blockers.Count == 0;
        return new(InvoiceApiDecision.Success, new(environment, ready, ready, signed,
            false, hasSnapshot, state.ToString(), revision, null, blockers.ToArray(),
            verified ? binding.MemoryId : null));
    }

}
