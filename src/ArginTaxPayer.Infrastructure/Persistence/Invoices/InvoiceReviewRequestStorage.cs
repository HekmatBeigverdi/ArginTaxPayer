namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>One durable internal review request/result, scoped to the exact invoice and actor.</summary>
public sealed class StoredInvoiceReviewRequest
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ActorUserId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string FingerprintSha256 { get; set; } = "";
    public long ResultReviewRevision { get; set; }
    public int ResultState { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}
