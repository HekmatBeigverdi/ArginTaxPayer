using ArginTaxPayer.Domain.Catalog;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06ReferenceVersioningTests
{
    private static readonly DateTime BaseTime =
        new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    private static CatalogReleaseIdentity Release(CatalogReleaseState state) =>
        new(Guid.NewGuid(), "RC_UMGS.ST", "V1.18",
            new string('a', 64), state, BaseTime);

    [Fact]
    public void Only_forward_release_state_transitions_are_permitted()
    {
        Assert.True(CatalogReleasePolicy.CanTransition(CatalogReleaseState.Staged, CatalogReleaseState.Validated));
        Assert.True(CatalogReleasePolicy.CanTransition(CatalogReleaseState.Validated, CatalogReleaseState.Active));
        Assert.True(CatalogReleasePolicy.CanTransition(CatalogReleaseState.Active, CatalogReleaseState.Superseded));
        foreach (var from in Enum.GetValues<CatalogReleaseState>())
        foreach (var to in Enum.GetValues<CatalogReleaseState>())
        {
            var expected = (from, to) is
                (CatalogReleaseState.Staged, CatalogReleaseState.Validated) or
                (CatalogReleaseState.Validated, CatalogReleaseState.Active) or
                (CatalogReleaseState.Active, CatalogReleaseState.Superseded);
            Assert.Equal(expected, CatalogReleasePolicy.CanTransition(from, to));
        }
    }

    [Fact]
    public void Only_staged_reference_entries_are_mutable()
    {
        Assert.True(CatalogReleasePolicy.IsReferenceWritable(CatalogReleaseState.Staged));
        Assert.False(CatalogReleasePolicy.IsReferenceWritable(CatalogReleaseState.Validated));
        Assert.False(CatalogReleasePolicy.IsReferenceWritable(CatalogReleaseState.Active));
        Assert.False(CatalogReleasePolicy.IsReferenceWritable(CatalogReleaseState.Superseded));
    }

    [Fact]
    public void Adjacent_half_open_intervals_do_not_overlap()
    {
        var a = BaseTime;
        var b = a.AddDays(10);
        var c = b.AddDays(10);
        Assert.False(CatalogReleasePolicy.Overlaps(a, b, b, c));
        Assert.True(CatalogReleasePolicy.Overlaps(a, b, b.AddTicks(-1), c));
        Assert.True(CatalogReleasePolicy.Overlaps(a, null, b, c));
        Assert.True(CatalogReleasePolicy.Overlaps(a, c, b, null));
        Assert.False(CatalogReleasePolicy.Overlaps(b, c, a, b));
        Assert.Throws<ArgumentException>(() => CatalogReleasePolicy.Overlaps(a, a, b, c));
        Assert.Throws<ArgumentException>(() => CatalogReleasePolicy.Overlaps(
            DateTime.SpecifyKind(a, DateTimeKind.Local), b, b, c));
    }

    [Fact]
    public void Snapshot_carries_immutable_source_provenance()
    {
        var release = Release(CatalogReleaseState.Active);
        var snapshot = CatalogReleasePolicy.Capture(release, "1627", "عدد", BaseTime.AddDays(1), BaseTime);
        Assert.Equal(release.Id, snapshot.ReleaseId);
        Assert.Equal(release.Sha256, snapshot.SourceSha256);
        Assert.Equal("V1.18", snapshot.Edition);
        Assert.Equal("1627", snapshot.Code);
        Assert.Equal("عدد", snapshot.Title);
        Assert.Equal(BaseTime, snapshot.LegalEffectiveAtUtc);
        Assert.Throws<ArgumentException>(() =>
            CatalogReleasePolicy.Capture(Release(CatalogReleaseState.Staged), "1627", "عدد",
                BaseTime, null));
    }

    [Fact]
    public void Invalid_source_provenance_is_not_accepted()
    {
        Assert.True(CatalogReleasePolicy.IsValidIdentity(Release(CatalogReleaseState.Staged)));
        Assert.False(CatalogReleasePolicy.IsValidIdentity(
            Release(CatalogReleaseState.Staged) with { Sha256 = "not-a-checksum" }));
        Assert.False(CatalogReleasePolicy.IsValidIdentity(
            Release(CatalogReleaseState.Staged) with { ImportedAtUtc =
                DateTime.SpecifyKind(BaseTime, DateTimeKind.Unspecified) }));
    }
}
