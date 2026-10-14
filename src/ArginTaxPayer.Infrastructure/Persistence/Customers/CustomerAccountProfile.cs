namespace ArginTaxPayer.Infrastructure.Persistence.Customers;

/// <summary>
/// Business-facing customer profile. Never a login credential, Party or TaxpayerProfile.
/// The IdentityUser record remains authoritative for activation and authentication.
/// </summary>
public sealed class CustomerAccountProfile
{
    public Guid CustomerUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public CustomerAccountProfileStatus Status { get; set; } = CustomerAccountProfileStatus.Active;
    public Guid? PreferredPartyId { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public enum CustomerAccountProfileStatus { Active = 1, Suspended = 2 }
