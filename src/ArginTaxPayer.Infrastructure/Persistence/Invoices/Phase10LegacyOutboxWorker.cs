using System.Text.Json;
using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Outbox;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Explicitly invoked Sandbox worker. It is not registered as a background
/// loop: an operator or a future queue host must invoke it deliberately.
/// </summary>
internal sealed class Phase10LegacyOutboxWorker(
    ArginTaxPayerDbContext db,
    SandboxLegacySigningKeyLoader keys,
    LegacyInvoiceOutboxSendCoordinator sender,
    Phase10DispatchAttemptRepository attempts,
    Phase10DispatchEvidenceRepository evidence,
    InvoiceDispatchFaultRepository faults,
    ILogger<Phase10LegacyOutboxWorker> logger)
    : IPhase10LegacyOutboxWorker
{
    public async Task<Phase10LegacyOutboxWorkResult> ProcessInvoiceAsync(
        Guid taxpayerId, Guid invoiceId, CancellationToken ct = default)
    {
        var messages = await db.OutboxMessages.AsNoTracking()
            .Where(x => x.TaxpayerId == taxpayerId &&
                        x.EventType == "invoice.sandbox.legacy.dispatch.requested" &&
                        x.ProcessedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        foreach (var message in messages)
        {
            using var payload = JsonDocument.Parse(message.PayloadJson,
                new JsonDocumentOptions { MaxDepth = 24 });
            if (payload.RootElement.TryGetProperty("invoiceId", out var field) &&
                Guid.TryParse(field.GetString(), out var candidate) && candidate == invoiceId)
                return await ProcessAsync(message.Id, ct);
        }
        return new("NotFound", "outbox_invoice_message_not_found");
    }

    private async Task<Phase10LegacyOutboxWorkResult> ProcessAsync(
        Guid messageId, CancellationToken ct = default)
    {
        var message = await db.OutboxMessages.SingleOrDefaultAsync(x =>
            x.Id == messageId && x.EventType == "invoice.sandbox.legacy.dispatch.requested", ct);
        if (message is null) return new("NotFound", "outbox_message_not_found");
        if (message.ProcessedAtUtc is not null) return new("AlreadyProcessed", "outbox_already_processed");

        using var payload = JsonDocument.Parse(message.PayloadJson,
            new JsonDocumentOptions { MaxDepth = 24 });
        var root = payload.RootElement;
        var operationId = GuidField(root, "operationId");
        var customer = GuidField(root, "customerUserId");
        var taxpayer = GuidField(root, "taxpayerId");
        var memory = GuidField(root, "fiscalMemoryId");
        // GET_TOKEN uses the six-character fiscal memory client ID, not
        // the 22-character invoice tax identifier.
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        string? fiscalMemoryCode;
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
SELECT memory_identifier FROM sandbox_fiscal_memories
WHERE id=@memory AND customer_user_id=@owner AND taxpayer_id=@taxpayer
""";
            foreach(var (name,value) in new (string,object)[] {
                ("memory",memory),("owner",customer),("taxpayer",taxpayer) })
            {
                var parameter=cmd.CreateParameter();
                parameter.ParameterName=name;
                parameter.Value=value;
                cmd.Parameters.Add(parameter);
            }
            fiscalMemoryCode = await cmd.ExecuteScalarAsync(ct) as string;
        }
        finally { if(opened) await connection.CloseAsync(); }
        if (fiscalMemoryCode is not { Length: 6 } ||
            fiscalMemoryCode.Any(c => !char.IsAsciiLetterOrDigit(c)))
            return new("Blocked", "sandbox_memory_client_id_unavailable");
        var key = await keys.RecoverAsync(customer, taxpayer, memory, ct);
        if (key is null) return new("Blocked", "sandbox_legacy_key_unavailable");
        using (key)
        {
            var prepared = await sender.PrepareAsync(message.PayloadJson, key, ct, fiscalMemoryCode);
            if (prepared is null)
                return new("Blocked", "sandbox_legacy_token_unavailable");

            var operation = await attempts.ReadStateAsync(operationId, customer, taxpayer, ct);
            if (operation is null || operation.State != InvoiceDispatchState.Reserved)
                return new("Blocked", "dispatch_not_reserved");
            var started = await attempts.BeginSendAsync(operationId, customer, taxpayer,
                operation.StateRevision, ct);
            if (started.Status != Phase10DispatchAttemptStartStatus.Started ||
                started.AttemptId is not { } attemptId || started.NewStateRevision is not { } sendingRevision)
                return new("Blocked", "dispatch_attempt_not_started");

            var result = await sender.SendPreparedAsync(prepared, ct);
            // Sanitized diagnostic: no token, signed payload, fiscal identifier, or provider body.
            logger.LogInformation(
                "Phase10 Sandbox enqueue outcome. OperationId={OperationId} AttemptId={AttemptId} Acknowledged={Acknowledged} SafeCode={SafeCode} HasReference={HasReference}",
                operationId, attemptId, result.Acknowledged, result.SafeCode,
                !string.IsNullOrWhiteSpace(result.ReferenceNumber));
            var observed = DateTime.UtcNow;
            if (result.Acknowledged && result.ReferenceNumber is not null)
            {
                var hash = Phase10DispatchEvidenceRepository.EvidenceHash(
                    result.SafeCode, result.ReferenceNumber, observed);
                var receipt = await evidence.AppendInitialReceiptAsync(operationId, customer,
                    taxpayer, attemptId, sendingRevision, result.ReferenceNumber, hash, observed, ct);
                if (receipt.Status != Phase10DispatchEvidenceStatus.Recorded)
                    return new("Blocked", "receipt_persistence_failed", attemptId);
                message.ProcessedAtUtc = observed;
                await db.SaveChangesAsync(ct);
                return new("Acknowledged", result.SafeCode, attemptId, result.ReferenceNumber);
            }

            var failure = await faults.RecordAsync(operationId, customer, taxpayer,
                InvoiceDispatchState.Sending, sendingRevision,
                InvoiceTransportFailureKind.UnknownAfterStart, ct);
            if (failure.Status != DispatchFaultPersistenceStatus.Recorded)
                return new("Indeterminate", "dispatch_outcome_unrecorded", attemptId);
            message.ProcessedAtUtc = observed;
            await db.SaveChangesAsync(ct);
            return new("Indeterminate", result.SafeCode, attemptId);
        }
    }

    private static Guid GuidField(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(field.GetString(), out var value) || value == Guid.Empty)
            throw new InvalidOperationException("Invalid scoped Outbox payload.");
        return value;
    }
}
