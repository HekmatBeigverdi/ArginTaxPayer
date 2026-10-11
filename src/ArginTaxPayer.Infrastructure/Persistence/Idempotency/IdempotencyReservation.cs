namespace ArginTaxPayer.Infrastructure.Persistence.Idempotency;

/// <summary>
/// Database reservation record; no external side effects or cached responses
/// are produced by this foundation.
/// </summary>
public sealed class IdempotencyReservation
{
    public Guid Id { get; set; }
    public Guid TaxpayerId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
