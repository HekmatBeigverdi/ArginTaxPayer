using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase18LegacyWorkResult(string Status, string SafeCode,
    string? ReferenceNumber = null);

/// <summary>
/// Explicitly invoked, single-attempt legacy Outbox processor. No background
/// retry or timer. Only an already-classified, immutable and prepared operation
/// is eligible. The historical Phase10 Sandbox worker remains untouched.
/// </summary>
internal sealed class Phase18ScopedLegacyOutboxWorker(
    ArginTaxPayerDbContext db,
    Phase18PersistedAuthorityOperationGate gate,
    Phase18AuthorityEndpointRoutingService routes,
    DatabaseLegacySigningKeyLoader keys,
    LegacyInvoiceAccessTokenClient tokens,
    Phase18DispatchAttemptAdmissionService admission,
    Phase18ScopedLegacyEnqueueTransport transport,
    Phase18DispatchOutcomeRepository outcomes)
{
    internal async Task<Phase18LegacyWorkResult> ProcessAsync(
        Guid operationId, Guid owner, Guid taxpayerId, Guid invoiceId,
        TaxAuthorityEnvironment provider, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(provider) || provider == TaxAuthorityEnvironment.Unknown)
            return new("Blocked", "unknown_authority_environment");

        var binding = await gate.ResolveAsync(operationId, owner, taxpayerId, invoiceId, ct);
        if (binding is not { } trusted || trusted.Environment != provider)
            return new("Blocked", "operation_environment_mismatch");

        var eventType = provider == TaxAuthorityEnvironment.Production
            ? "invoice.production.legacy.dispatch.requested"
            : "invoice.sandbox.legacy.dispatch.requested";
        var message = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == operationId && x.TaxpayerId == taxpayerId &&
            x.EventType == eventType && x.ProcessedAtUtc == null, ct);
        if (message is null)
            return new("Blocked", "classified_outbox_not_found");

        string payloadMemory;
        try
        {
            using var parsed = JsonDocument.Parse(message.PayloadJson,
                new JsonDocumentOptions { MaxDepth = 24 });
            var root=parsed.RootElement;
            var expectedEnvironment = provider == TaxAuthorityEnvironment.Production
                ? "Production" : "Sandbox";
            var expectedSchema = provider == TaxAuthorityEnvironment.Production
                ? "phase18.production.legacy.invoice.dispatch.v1"
                : "phase10.sandbox.legacy.invoice.dispatch.v1";
            if (root.GetProperty("schema").GetString() != expectedSchema ||
                root.GetProperty("environment").GetString() != expectedEnvironment ||
                root.GetProperty("operationId").GetString() != operationId.ToString("D") ||
                root.GetProperty("invoiceId").GetString() != invoiceId.ToString("D") ||
                root.GetProperty("customerUserId").GetString() != owner.ToString("D") ||
                root.GetProperty("taxpayerId").GetString() != taxpayerId.ToString("D") ||
                root.GetProperty("fiscalMemoryId").GetString() != trusted.FiscalMemoryId.ToString("D"))
                return new("Blocked", "outbox_scope_mismatch");
            payloadMemory = root.GetProperty("fiscalMemoryId").GetString()!;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or KeyNotFoundException)
        {
            return new("Blocked", "outbox_payload_invalid");
        }

        var memoryCode = await db.FiscalMemories.AsNoTracking()
            .Where(m => m.Id == trusted.FiscalMemoryId &&
                m.TaxpayerId == taxpayerId)
            .Select(m => m.MemoryIdentifier).SingleOrDefaultAsync(ct);
        if (payloadMemory != trusted.FiscalMemoryId.ToString("D") ||
            memoryCode is not { Length: 6 })
            return new("Blocked", "fiscal_memory_identifier_unavailable");

        var tokenUrl = await routes.ResolveAsync(operationId, owner, taxpayerId,
            invoiceId, provider, "GET_TOKEN", ct);
        if (tokenUrl is null)
            return new("Blocked", "token_route_disabled");

        using var key = await keys.RecoverAsync(owner, taxpayerId,
            trusted.FiscalMemoryId, ct);
        if (key is null)
            return new("Blocked", "scoped_signing_key_unavailable");

        var token = await tokens.RequestVerifiedEndpointAsync(tokenUrl, memoryCode,
            key.PrivateKey, key.PublicSpkiDer, ct);
        if (!token.Authenticated || token.AccessToken is null)
            return new("Blocked", "authority_token_unavailable");

        LegacyInvoiceSubmissionPacket packet;
        try
        {
            packet = LegacyInvoiceOutboxPacketRebuilder.Rebuild(
                message.PayloadJson, key.PrivateKey, key.PublicSpkiDer,
                token.AccessToken, ct, memoryCode, provider);
        }
        catch (Exception exception) when (exception is ArgumentException or
            JsonException or InvalidOperationException)
        {
            return new("Blocked", "signed_outbox_package_invalid");
        }

        var state = await db.Database.SqlQueryRaw<long?>("""
SELECT state_revision AS "Value" FROM invoice_dispatch_operations
WHERE id={0} AND customer_user_id={1} AND taxpayer_id={2}
 AND invoice_id={3} AND state=3
""", operationId, owner, taxpayerId, invoiceId).SingleOrDefaultAsync(ct);
        if (state is not { } expectedState)
            return new("Blocked", "operation_not_reserved");

        var claim = await admission.ClaimAsync(operationId, owner,
            taxpayerId, invoiceId, expectedState, provider, ct);
        if (claim.Decision != Phase18DispatchAttemptDecision.Started ||
            claim.AttemptId is not { } attemptId ||
            claim.StateRevision is not { } sendingRevision)
            return new("Blocked", claim.BlockingCode ?? "operation_attempt_not_admitted");

        // No work or automatic resend after this durable point. Any failure
        // after attempt creation is classified as Indeterminate.
        LegacyInvoiceSubmissionHttpTransport.Result response;
        try
        {
            response = await transport.PostOnceAsync(operationId, owner,
                taxpayerId, invoiceId, provider, packet.FiscalTaxId,
                packet.RequestJson, packet.TransferSignature, token.AccessToken,
                packet.RequestTraceId, packet.Timestamp, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or
            JsonException or InvalidOperationException or OperationCanceledException)
        {
            response = new(false, "legacy_outcome_indeterminate");
        }

        var acknowledged = response.Acknowledged &&
            !string.IsNullOrWhiteSpace(response.ReferenceNumber);
        var outcome = await outcomes.RecordAsync(operationId, owner, taxpayerId,
            invoiceId, attemptId, sendingRevision, provider, acknowledged,
            acknowledged ? "legacy_reference_acknowledged" : "legacy_outcome_indeterminate",
            acknowledged ? response.ReferenceNumber : null, ct);
        if (outcome.Decision != Phase18DispatchOutcomeDecision.Recorded)
            return new("Indeterminate", "outcome_persistence_requires_reconciliation");

        // Mark only this exact event consumed. Failure to update it cannot
        // permit a second send because durable attempt/state CAS has advanced.
        await db.OutboxMessages.Where(o => o.Id == operationId &&
                o.TaxpayerId == taxpayerId && o.EventType == eventType &&
                o.ProcessedAtUtc == null)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(o => o.ProcessedAtUtc, DateTime.UtcNow), ct);
        return acknowledged
            ? new("Acknowledged", "legacy_reference_acknowledged", response.ReferenceNumber)
            : new("Indeterminate", "legacy_outcome_indeterminate");
    }
}
