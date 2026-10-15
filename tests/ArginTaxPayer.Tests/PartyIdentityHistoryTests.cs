using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PartyIdentityHistoryTests
{
    [Fact]
    public void Captures_original_legal_identity_and_revision_before_conversion()
    {
        var original = new Party
        {
            Id = Guid.NewGuid(), Kind = PartyKind.NaturalPerson,
            NormalizedIdentifier = "0087654321", FirstName = "علی",
            LastName = "سهرابی", DisplayName = "علی سهرابی", Revision = 7,
            Status = PartyStatus.Active
        };
        var actor = Guid.NewGuid();
        var time = DateTime.UtcNow;
        var history = PartyIdentityEditService.CapturePrevious(original, actor, time);
        original.Kind = PartyKind.LegalEntity;
        original.NormalizedIdentifier = "12345678901";
        original.DisplayName = "شرکت نمونه";
        original.Revision++;
        Assert.Equal(PartyKind.NaturalPerson, history.PriorKind);
        Assert.Equal("0087654321", history.PriorNormalizedIdentifier);
        Assert.Equal("علی سهرابی", history.PriorDisplayName);
        Assert.Equal(7, history.PriorRevision);
        Assert.Equal(actor, history.ActorUserId);
        Assert.Equal(original.Id, history.PartyId);
        Assert.Equal(time, history.CapturedAtUtc);
        Assert.Equal(PartyKind.LegalEntity, original.Kind);
    }
}
