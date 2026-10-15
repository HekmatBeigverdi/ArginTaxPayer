namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>Optional organization branding and mandatory first-use identification for a login account.
/// Never a buyer Party or seller taxpayer identity.</summary>
public sealed class SoftwareUserProfile
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? CompanyName { get; set; }
    public string Address { get; set; } = "";
    public byte[]? InvoiceLogoPng { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime CompletedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
