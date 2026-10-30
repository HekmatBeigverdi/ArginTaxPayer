using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Phase10 Step10 transport model. A receipt is only a provider acknowledgement;
/// it is never legal acceptance. These types contain no sensitive packet content.
/// </summary>
public enum InvoiceSubmissionReceiptStatus
{
    Acknowledged = 1,
    ExplicitlyRejected = 2,
    Indeterminate = 3
}

public sealed record InvoiceSubmissionReceipt(
    Guid OperationId, string? ProviderReference, InvoiceSubmissionReceiptStatus Status,
    string SafeCode, DateTime ObservedAtUtc)
{
    public bool IsFiscalAccepted => false;
}

/// <summary>
/// An immutable, safe descriptor of a packet reserved by the durable outbox.
/// The actual protected packet and signing proof are intentionally NOT accepted
/// by this Step10 API. No production adapter is registered.
/// </summary>
public sealed record InvoiceSubmissionDispatchDescriptor(
    Guid OperationId, Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId,
    Guid InvoiceId, long InvoiceRevision, string WireSha256,
    string IdempotencyKey, string EndpointCode);

public sealed record InvoiceSubmissionTransportOutcome(
    Guid OperationId, InvoiceSubmissionReceiptStatus Status,
    string? ProviderReference, string SafeCode, DateTime ObservedAtUtc);

public interface IInvoiceSubmissionTransport
{
    Task<InvoiceSubmissionTransportOutcome> SendAsync(
        InvoiceSubmissionDispatchDescriptor descriptor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deliberate production fail-closed implementation, with no HTTP client,
/// signing key, database transaction or authority endpoint.
/// </summary>
public sealed class DisabledInvoiceSubmissionTransport : IInvoiceSubmissionTransport
{
    public Task<InvoiceSubmissionTransportOutcome> SendAsync(
        InvoiceSubmissionDispatchDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(descriptor);
        return Task.FromResult(new InvoiceSubmissionTransportOutcome(descriptor.OperationId,
            InvoiceSubmissionReceiptStatus.Indeterminate, null,
            "invoice.dispatch.transport_disabled", DateTime.UtcNow));
    }
}

/// <summary>
/// Pure receipt interpretation, independent of provider response JSON.
/// Future HTTP adapters must validate the official packet schema/correlation and
/// classify failures before invoking this. A nonempty receipt reference alone
/// does not mean acceptance by the Tax Authority.
/// </summary>
public static class InvoiceSubmissionReceiptPolicy
{
    private static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    public static InvoiceSubmissionReceipt Parse(
        InvoiceSubmissionDispatchDescriptor descriptor,
        InvoiceSubmissionTransportOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(outcome);
        if (descriptor.OperationId == Guid.Empty || descriptor.InvoiceId == Guid.Empty ||
            descriptor.CustomerUserId == Guid.Empty || descriptor.TaxpayerId == Guid.Empty ||
            descriptor.FiscalMemoryId == Guid.Empty || descriptor.InvoiceRevision < 1 ||
            !IsSha256(descriptor.WireSha256) ||
            string.IsNullOrWhiteSpace(descriptor.IdempotencyKey) ||
            descriptor.IdempotencyKey.Length > 128 ||
            descriptor.EndpointCode != "tax-authority-submit" ||
            outcome.OperationId != descriptor.OperationId ||
            outcome.ObservedAtUtc.Kind != DateTimeKind.Utc ||
            !Enum.IsDefined(outcome.Status))
            throw new ArgumentException("Invalid scoped dispatch receipt evidence.");

        if (string.IsNullOrWhiteSpace(outcome.SafeCode) ||
            outcome.SafeCode.Length > 100 ||
            outcome.SafeCode.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.')))
            throw new ArgumentException("Invalid safe receipt code.");

        var reference = outcome.ProviderReference;
        if (reference is not null &&
            (reference.Length is < 1 or > 128 ||
             reference.Any(c => !(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))))
            throw new ArgumentException("Invalid bounded provider reference.");

        if (outcome.Status == InvoiceSubmissionReceiptStatus.Acknowledged &&
            string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Acknowledgement requires a bounded reference.");
        if (outcome.Status != InvoiceSubmissionReceiptStatus.Acknowledged && reference is not null)
            throw new ArgumentException("Only acknowledged operations carry a receipt reference.");
        return new InvoiceSubmissionReceipt(descriptor.OperationId, reference,
            outcome.Status, outcome.SafeCode, outcome.ObservedAtUtc);
    }

    public static InvoiceDispatchEvent? SuggestedEvent(InvoiceSubmissionReceiptStatus status) =>
        status switch
        {
            InvoiceSubmissionReceiptStatus.Acknowledged => InvoiceDispatchEvent.ReceiptReceived,
            InvoiceSubmissionReceiptStatus.ExplicitlyRejected => InvoiceDispatchEvent.DefinitiveFailure,
            InvoiceSubmissionReceiptStatus.Indeterminate => InvoiceDispatchEvent.OutcomeUnknown,
            _ => null
        };
}

/// <summary>
/// A test-only transport accepts an explicit injected response handler. Not
/// registered in production DI. It performs no HTTP or persistence.
/// </summary>
public sealed class SimulatedInvoiceSubmissionTransport(
    Func<InvoiceSubmissionDispatchDescriptor, InvoiceSubmissionTransportOutcome> callback)
    : IInvoiceSubmissionTransport
{
    public Task<InvoiceSubmissionTransportOutcome> SendAsync(
        InvoiceSubmissionDispatchDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(descriptor);
        return Task.FromResult(callback(descriptor));
    }
}
