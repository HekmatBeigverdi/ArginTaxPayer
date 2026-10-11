namespace ArginTaxPayer.Infrastructure.Persistence.Outbox;

/// <summary>Durable payload for future internal dispatch. No external sends in Step 12.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid TaxpayerId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
}
