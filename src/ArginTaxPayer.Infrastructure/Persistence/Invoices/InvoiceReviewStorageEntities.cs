using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Phase08 review cursor, separately revisioned from fiscal invoice documents.</summary>
public sealed class StoredInvoiceReview
{
    public Guid InvoiceId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public InvoiceReviewState State { get; set; }
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? LastActorUserId { get; set; }
}

/// <summary>Append-only internal review audit event. Never authority status.</summary>
public sealed class StoredInvoiceReviewEvent
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public long Revision { get; set; }
    public InvoiceReviewState FromState { get; set; }
    public InvoiceReviewState ToState { get; set; }
    public InvoiceReviewAction Action { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? ChangeReason { get; set; }
}
