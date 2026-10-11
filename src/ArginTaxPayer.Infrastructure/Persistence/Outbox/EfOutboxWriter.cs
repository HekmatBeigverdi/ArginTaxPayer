using ArginTaxPayer.Application.Abstractions.Messaging;
using ArginTaxPayer.Application.Abstractions.Tenancy;

namespace ArginTaxPayer.Infrastructure.Persistence.Outbox;

/// <summary>
/// Adds a message to the current DbContext, but does not commit.
/// Call IUnitOfWork.SaveChangesAsync (or ExecuteInTransactionAsync) with the
/// corresponding business writes to preserve atomicity.
/// </summary>
public sealed class EfOutboxWriter(
    ArginTaxPayerDbContext dbContext,
    ITaxpayerScope taxpayerScope) : IOutboxWriter
{
    public Task EnqueueAsync(
        Guid messageId,
        string eventType,
        string payloadJson,
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (messageId == Guid.Empty)
            throw new ArgumentException("Message ID must not be empty.", nameof(messageId));
        if (string.IsNullOrWhiteSpace(eventType) || eventType.Length > 200)
            throw new ArgumentException("Event type must contain 1-200 characters.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("Payload JSON is required.", nameof(payloadJson));
        if (string.IsNullOrWhiteSpace(deduplicationKey) || deduplicationKey.Length > 200)
            throw new ArgumentException("Deduplication key must contain 1-200 characters.", nameof(deduplicationKey));

        var taxpayerId = taxpayerScope.RequireTaxpayerId();
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = messageId,
            TaxpayerId = taxpayerId,
            EventType = eventType,
            PayloadJson = payloadJson,
            DeduplicationKey = deduplicationKey,
            CreatedAtUtc = DateTime.UtcNow,
            AttemptCount = 0
        });
        return Task.CompletedTask;
    }
}
