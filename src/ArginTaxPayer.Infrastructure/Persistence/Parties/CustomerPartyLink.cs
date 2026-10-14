namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

/// <summary>Explicit customer-to-party relationship. Not a commercial partner grant or taxpayer binding.</summary>
public sealed class CustomerPartyLink
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid PartyId { get; set; }
    public DateTime LinkedAtUtc { get; set; }
    public DateTime? UnlinkedAtUtc { get; set; }
}
