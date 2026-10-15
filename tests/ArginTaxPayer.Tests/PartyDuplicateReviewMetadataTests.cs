using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PartyDuplicateReviewMetadataTests
{
    [Fact]
    public void Review_is_append_only_audit_record_with_restricted_references()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=not_connected;Username=none;Password=none")
                .Options);
        var entity = db.Model.FindEntityType(typeof(PartyDuplicateReview))!;
        Assert.Equal("party_duplicate_reviews", entity.GetTableName());
        Assert.Equal(4, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(entity.GetIndexes(), idx => idx.Properties
            .Select(p => p.Name).SequenceEqual(new[]
                { nameof(PartyDuplicateReview.CustomerUserId), nameof(PartyDuplicateReview.ReviewedAtUtc) }));
        Assert.Equal(500, entity.FindProperty(nameof(PartyDuplicateReview.Reason))!.GetMaxLength());
        Assert.False(entity.FindProperty(nameof(PartyDuplicateReview.ReviewerUserId))!.IsNullable);
    }

    [Fact]
    public void Human_review_has_only_non_mutating_decisions()
    {
        Assert.Equal(new[] { PartyDuplicateDecision.ConfirmedDuplicate, PartyDuplicateDecision.NotDuplicate },
            Enum.GetValues<PartyDuplicateDecision>());
    }
}
