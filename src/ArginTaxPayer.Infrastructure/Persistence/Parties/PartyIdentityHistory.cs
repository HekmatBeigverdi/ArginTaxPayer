namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

/// <summary>
/// Immutable snapshot of the master Party identity BEFORE an authorized edit.
/// This is audit history, NOT an issued-invoice snapshot or substitute for the
/// original payload received by the tax authority.
/// </summary>
public sealed class PartyIdentityHistory
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public Guid ActorUserId { get; set; }
    public long PriorRevision { get; set; }
    public PartyKind PriorKind { get; set; }
    public string PriorNormalizedIdentifier { get; set; } = "";
    public string PriorDisplayName { get; set; } = "";
    public string? PriorFirstName { get; set; }
    public string? PriorLastName { get; set; }
    public string? PriorRegistrationNumber { get; set; }
    public DateTime CapturedAtUtc { get; set; }
}
