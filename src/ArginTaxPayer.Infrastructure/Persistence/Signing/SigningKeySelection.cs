namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Metadata only. Never store private keys, passwords or envelope bytes here.</summary>
public sealed class SigningKeySelection
{
    public Guid FiscalMemoryId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid? KeyVersionId { get; set; }
    public long Revision { get; set; }
    public DateTime SelectedAtUtc { get; set; }
    public Guid SelectedByUserId { get; set; }
}
