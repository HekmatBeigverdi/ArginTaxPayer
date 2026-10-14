namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

/// <summary>Fiscal invoice header metadata only. Never credentials or proof of tax authority verification.</summary>
public sealed class InvoiceParticipantDetails
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    // Exactly one owner: buyer economic person or seller fiscal taxpayer profile.
    public Guid? PartyId { get; set; }
    public Guid? TaxpayerId { get; set; }
    public string? EconomicCode { get; set; }
    public string? BranchCode { get; set; }
    public string? PostalCode { get; set; }
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ForeignNationalCode { get; set; }
    public string? PassportNumber { get; set; }
    public string? ContractReference { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
