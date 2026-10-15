namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public enum PartyContactKind { Mobile = 1, Telephone = 2, Email = 3 }

/// <summary>Party contact history is retained through soft deactivation.</summary>
public sealed class PartyContact
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public PartyContactKind Kind { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
}
