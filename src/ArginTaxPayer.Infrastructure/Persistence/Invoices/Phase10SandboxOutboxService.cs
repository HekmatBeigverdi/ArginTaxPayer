using System.Text.Json;
using ArginTaxPayer.Application.Abstractions.Messaging;
using ArginTaxPayer.Application.Abstractions.Persistence;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum Phase10SandboxOutboxPreparationStatus
{
    Prepared = 1,
    Rejected = 2
}

public sealed record Phase10SandboxOutboxPreparationResult(
    Phase10SandboxOutboxPreparationStatus Status,
    Guid MessageId,
    string DeduplicationKey,
    string? Code = null);

/// <summary>
/// Commits only an encrypted, immutable Sandbox packet descriptor to the
/// existing durable outbox. It never calls an authority transport. The worker
/// that claims this event must perform the single external POST only after the
/// outbox transaction has committed and must record an indeterminate outcome on
/// timeout instead of retrying blindly.
/// </summary>
public sealed class Phase10SandboxOutboxService(
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork)
{
    public async Task<Phase10SandboxOutboxPreparationResult> PrepareAsync(
        InvoiceDispatchAdmission admission,
        Phase10SandboxInvoicePacket packet,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.InvoiceId != admission.InvoiceId ||
            packet.CustomerUserId != admission.CustomerUserId ||
            packet.TaxpayerId != admission.TaxpayerId ||
            packet.SandboxFiscalMemoryId != admission.FiscalMemoryId ||
            packet.InvoiceRevision != admission.InvoiceRevision ||
            !string.Equals(packet.JweSha256, admission.PayloadSha256,
                StringComparison.OrdinalIgnoreCase))
            return new(Phase10SandboxOutboxPreparationStatus.Rejected,
                Guid.Empty, string.Empty, "packet_identity_mismatch");

        var messageId = admission.OperationId;
        var deduplicationKey =
            $"phase10:sandbox:{admission.TaxpayerId:N}:{admission.InvoiceId:N}:{admission.InvoiceRevision}";
        var payload = JsonSerializer.Serialize(new
        {
            schema = "phase10.sandbox.invoice.dispatch.v1",
            environment = "Sandbox",
            operationId = admission.OperationId,
            customerUserId = packet.CustomerUserId,
            taxpayerId = packet.TaxpayerId,
            sandboxFiscalMemoryId = packet.SandboxFiscalMemoryId,
            invoiceId = packet.InvoiceId,
            invoiceRevision = packet.InvoiceRevision,
            fiscalTaxId = packet.FiscalTaxId,
            requestTraceId = packet.RequestTraceId,
            signingKeyFingerprintSha256 = packet.SigningKeyFingerprintSha256,
            authorityEncryptionKeyId = packet.AuthorityEncryptionKeyId,
            jwe = packet.Jwe,
            jweSha256 = packet.JweSha256,
            jwsSha256 = packet.JwsSha256,
            endpointCode = "tax-authority-submit",
            automaticRetry = false
        });

        await unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            await outbox.EnqueueAsync(messageId,
                "invoice.sandbox.dispatch.requested", payload,
                deduplicationKey, transactionCt);
        }, ct);
        return new(Phase10SandboxOutboxPreparationStatus.Prepared,
            messageId, deduplicationKey);
    }
}
