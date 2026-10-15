namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

/// <summary>Append-only human review decision; it never merges or changes ownership.</summary>
public sealed class PartyDuplicateReview
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid CandidatePartyId { get; set; }
    public Guid ExistingPartyId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public PartyDuplicateDecision Decision { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ReviewedAtUtc { get; set; }
}
public enum PartyDuplicateDecision { ConfirmedDuplicate = 1, NotDuplicate = 2 }
