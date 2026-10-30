using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Phase10 Step05 immutable persistence admission identity. This is a pure
/// schema-planning contract, not an EF repository, SQL migration or live sender.
/// The database must independently enforce scoped unique constraints.
/// </summary>
public sealed record InvoiceDispatchAdmission
{
    public Guid OperationId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public Guid FiscalMemoryId { get; }
    public Guid InvoiceId { get; }
    public long InvoiceRevision { get; }
    public string PayloadSha256 { get; }
    public string IdempotencyKey { get; }

    private InvoiceDispatchAdmission(Guid operationId, Guid customerUserId, Guid taxpayerId,
        Guid fiscalMemoryId, Guid invoiceId, long invoiceRevision, string payloadSha256,
        string idempotencyKey)
    {
        OperationId = operationId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        FiscalMemoryId = fiscalMemoryId;
        InvoiceId = invoiceId;
        InvoiceRevision = invoiceRevision;
        PayloadSha256 = payloadSha256;
        IdempotencyKey = idempotencyKey;
    }

    public static InvoiceDispatchAdmission Create(Guid operationId, Guid customerUserId,
        Guid taxpayerId, Guid fiscalMemoryId, Guid invoiceId, long invoiceRevision,
        string payloadSha256, string idempotencyKey)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || fiscalMemoryId == Guid.Empty ||
            invoiceId == Guid.Empty || invoiceRevision <= 0 ||
            !InvoiceValidationReceipt.IsSha256(payloadSha256) ||
            string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128 ||
            idempotencyKey.Any(c => c is < (char)0x21 or > (char)0x7e))
            throw new ArgumentException("Invalid scoped dispatch admission.");
        return new(operationId, customerUserId, taxpayerId, fiscalMemoryId, invoiceId,
            invoiceRevision, payloadSha256.ToLowerInvariant(), idempotencyKey);
    }

    public InvoiceAdmissionReplayDecision CompareReplay(InvoiceDispatchAdmission other)
    {
        ArgumentNullException.ThrowIfNull(other);
        // The idempotency namespace belongs to the authenticated user + taxpayer.
        if (other.CustomerUserId != CustomerUserId || other.TaxpayerId != TaxpayerId ||
            !string.Equals(other.IdempotencyKey, IdempotencyKey, StringComparison.Ordinal))
            return InvoiceAdmissionReplayDecision.DifferentKeyOrScope;

        return other.FiscalMemoryId == FiscalMemoryId && other.InvoiceId == InvoiceId &&
               other.InvoiceRevision == InvoiceRevision &&
               string.Equals(other.PayloadSha256, PayloadSha256, StringComparison.Ordinal)
            ? InvoiceAdmissionReplayDecision.ReplaySameOperation
            : InvoiceAdmissionReplayDecision.ConflictingPayload;
    }
}

public enum InvoiceAdmissionReplayDecision
{
    ReplaySameOperation = 1,
    ConflictingPayload = 2,
    DifferentKeyOrScope = 3
}

/// <summary>
/// A durable row must first prove the state and any official reconciliation
/// evidence. A lease expiry or local HTTP timeout NEVER proves non-delivery.
/// </summary>
public static class InvoiceDispatchStoragePolicy
{
    public static bool EligibleForDirectNewSend(InvoiceDispatchState state) =>
        state == InvoiceDispatchState.Reserved;

    public static bool RequiresExternalResolution(InvoiceDispatchState state) =>
        state is InvoiceDispatchState.Sending or InvoiceDispatchState.Indeterminate;

    public static bool CanRedriveOutboxWithoutReconciliation(InvoiceDispatchState state) =>
        state == InvoiceDispatchState.Reserved;

    public static bool CanRemovePersistedAttempt(InvoiceDispatchState state) => false;
}
