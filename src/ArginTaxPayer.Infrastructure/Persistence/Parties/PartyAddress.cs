namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public sealed class PartyAddress
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "IR";
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
}
