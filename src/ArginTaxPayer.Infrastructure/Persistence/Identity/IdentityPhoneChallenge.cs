namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>Purpose-specific one-use proof. Codes are only stored as keyed digests.</summary>
public sealed class IdentityPhoneChallenge
{
    public Guid Id { get; set; }
    public string Mobile { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string CodeDigest { get; set; } = "";
    public Guid? UserId { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public bool Delivered { get; set; }
}
