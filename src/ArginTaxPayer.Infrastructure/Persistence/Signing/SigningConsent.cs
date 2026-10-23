namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Metadata only. Never store private keys, passwords or envelope bytes here.</summary>
public sealed class SigningConsent
{
    public Guid Id { get; set; }
    public Guid OperationRecordId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid FiscalMemoryId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ConsentVersion { get; set; } = string.Empty;
    public DateTime ReauthenticatedAtUtc { get; set; }
    public DateTime ConsentedAtUtc { get; set; }
}
