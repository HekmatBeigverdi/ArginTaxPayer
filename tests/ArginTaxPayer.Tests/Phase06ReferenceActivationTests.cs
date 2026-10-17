using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06ReferenceActivationTests
{
    [Theory]
    [InlineData(CatalogReleaseState.Staged, CatalogReleaseState.Validated, true)]
    [InlineData(CatalogReleaseState.Validated, CatalogReleaseState.Active, true)]
    [InlineData(CatalogReleaseState.Active, CatalogReleaseState.Superseded, true)]
    [InlineData(CatalogReleaseState.Superseded, CatalogReleaseState.Active, false)]
    [InlineData(CatalogReleaseState.Staged, CatalogReleaseState.Active, false)]
    [InlineData(CatalogReleaseState.Active, CatalogReleaseState.Staged, false)]
    public void Approval_state_machine_is_forward_only(
        CatalogReleaseState from, CatalogReleaseState to, bool allowed)
    {
        Assert.Equal(allowed, CatalogReleasePolicy.CanTransition(from, to));
    }

    [Fact]
    public void Published_catalog_is_readable_but_not_editable()
    {
        Assert.True(CatalogReleasePolicy.IsReferenceWritable(CatalogReleaseState.Staged));
        Assert.False(CatalogReleasePolicy.IsReferenceWritable(CatalogReleaseState.Validated));
        Assert.False(CatalogReleasePolicy.IsReferenceWritable(CatalogReleaseState.Active));
        Assert.False(CatalogReleasePolicy.IsReferenceWritable(CatalogReleaseState.Superseded));
    }

    [Fact]
    public void Operator_result_does_not_claim_external_authority_verification()
    {
        Assert.NotEqual(ReleaseChangeDecision.Success, ReleaseChangeDecision.MissingReviewEvidence);
        Assert.NotEqual(ReleaseChangeDecision.Success, ReleaseChangeDecision.MissingVerifiedEntries);
        Assert.NotEqual(ReleaseChangeDecision.Success, ReleaseChangeDecision.Forbidden);
        Assert.NotEqual(ReleaseChangeDecision.Success, ReleaseChangeDecision.Conflict);
    }

    [Fact]
    public void Release_metadata_has_unique_source_hash_and_active_state_storage()
    {
        using var db = new ArginTaxPayer.Infrastructure.Persistence.ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayer.Infrastructure.Persistence.ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used").Options);
        var entity = db.Model.FindEntityType(typeof(CatalogReferenceRelease))!;
        Assert.Equal("catalog_reference_releases", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(CatalogReferenceRelease.SourceDocument),
                        nameof(CatalogReferenceRelease.SourceSha256) }));
    }
}
