namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

/// <summary>Economic actor; independent from an authenticated customer and fiscal taxpayer.</summary>
public sealed class Party
{
    public Guid Id { get; set; }
    public PartyKind Kind { get; set; }
    public string NormalizedIdentifier { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PartyStatus Status { get; set; } = PartyStatus.Active;
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public enum PartyKind { NaturalPerson = 1, LegalEntity = 2 }
public enum PartyStatus { Active = 1, Archived = 2 }
