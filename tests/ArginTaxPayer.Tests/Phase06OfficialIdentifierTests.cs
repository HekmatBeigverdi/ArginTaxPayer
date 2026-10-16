using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06OfficialIdentifierTests
{
    [Theory]
    [InlineData("1234567890123", "1234567890123")]
    [InlineData("۱۲۳۴۵۶۷۸۹۰۱۲۳", "1234567890123")]
    [InlineData("١٢٣٤٥٦٧٨٩٠١٢٣", "1234567890123")]
    [InlineData(" 0000000000123 ", "0000000000123")]
    public void Normalizes_official_sstid_without_losing_leading_zeroes(string input, string expected)
    {
        Assert.True(OfficialGoodsServiceIdentifierPolicy.TryNormalize(input, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456789012")]
    [InlineData("12345678901234")]
    [InlineData("123456789012a")]
    [InlineData("123456-890123")]
    [InlineData("12345678901\n3")]
    [InlineData("１２３４５６７８９０１２３")]
    public void Rejects_invalid_official_sstid(string? input)
    {
        Assert.False(OfficialGoodsServiceIdentifierPolicy.TryNormalize(input, out var code));
        Assert.Equal(string.Empty, code);
    }

    [Fact]
    public void Structural_format_does_not_imply_tax_authority_approval()
    {
        Assert.True(OfficialGoodsServiceIdentifierPolicy.TryNormalize("1234567890123", out _));
        Assert.False(OfficialGoodsServiceReferencePolicy.CanBeTreatedAsVerified(
            OfficialVerificationStatus.Unverified, 3, true));
        Assert.False(OfficialGoodsServiceReferencePolicy.CanBeTreatedAsVerified(
            OfficialVerificationStatus.Verified, 1, true));
        Assert.False(OfficialGoodsServiceReferencePolicy.CanBeTreatedAsVerified(
            OfficialVerificationStatus.Verified, 3, false));
        Assert.True(OfficialGoodsServiceReferencePolicy.CanBeTreatedAsVerified(
            OfficialVerificationStatus.Verified, 3, true));
    }

    [Theory]
    [InlineData(1, OfficialEntryKind.Goods, true)]
    [InlineData(2, OfficialEntryKind.Goods, true)]
    [InlineData(3, OfficialEntryKind.Service, true)]
    [InlineData(1, OfficialEntryKind.Service, false)]
    [InlineData(3, OfficialEntryKind.Goods, false)]
    [InlineData(5, OfficialEntryKind.Goods, false)]
    public void ArginAccounting_item_kind_maps_only_to_matching_official_class(
        int itemKind, OfficialEntryKind officialKind, bool expected)
    {
        Assert.Equal(expected, OfficialGoodsServiceReferencePolicy.IsCompatible(itemKind, officialKind));
    }

    [Theory]
    [InlineData("نصب کولر", true)]
    [InlineData(" ", false)]
    [InlineData("a", false)]
    [InlineData("خدمت\u0001", false)]
    public void Candidate_title_follows_safe_normalization(string input, bool expected)
    {
        Assert.Equal(expected, OfficialGoodsServiceIdentifierPolicy.TryValidateTitle(input, out _));
    }

    [Fact]
    public void Database_preserves_unverified_state_and_release_scoped_uniqueness()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=never_used;Username=never_used;Password=never_used").Options);
        var entity = db.Model.FindEntityType(typeof(OfficialGoodsServiceEntry))!;
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(OfficialGoodsServiceEntry.ReleaseId),
                        nameof(OfficialGoodsServiceEntry.Kind),
                        nameof(OfficialGoodsServiceEntry.Code) }));
        Assert.Equal(1, (int)OfficialVerificationStatus.Unverified);
        Assert.Equal(2, (int)OfficialVerificationStatus.Verified);
    }
}
