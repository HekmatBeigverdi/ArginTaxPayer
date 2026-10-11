namespace ArginTaxPayer.Application.Abstractions.Messaging;

/// <summary>
/// Enqueues a pending durable event. Callers must commit it with the business
/// changes in the same Unit of Work; no delivery is implied by enqueue.
/// </summary>
public interface IOutboxWriter
{
    Task EnqueueAsync(
        Guid messageId,
        string eventType,
        string payloadJson,
        string deduplicationKey,
        CancellationToken cancellationToken = default);
}
