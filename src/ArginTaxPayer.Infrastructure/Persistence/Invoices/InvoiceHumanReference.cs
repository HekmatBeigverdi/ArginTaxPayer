namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Immutable human-readable invoice support reference.</summary>
public sealed class InvoiceHumanReference
{
    public Guid Id { get; set; }
    public string SourceKind { get; set; } = "";
    public Guid SourceId { get; set; }
    public Guid AdminUserId { get; set; }
    public short PersianYear { get; set; }
    public int Serial { get; set; }
    public string SystemCode { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; }
}
