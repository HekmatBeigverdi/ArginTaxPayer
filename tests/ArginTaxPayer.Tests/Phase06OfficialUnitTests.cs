using System.Reflection;
using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06OfficialUnitTests
{
    private static (int Row, string Code, string Title)[] SourceRows()
    {
        var field = typeof(SeedOfficialUnitsV118).GetField("SourceRows",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var text = (string)field.GetRawConstantValue()!;
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => {
                var parts = line.Split('|');
                Assert.Equal(3, parts.Length);
                return (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), parts[1], parts[2]);
            }).ToArray();
    }

    [Fact]
    public void Owner_provided_v118_has_exactly_102_distinct_ordered_official_units()
    {
        var rows = SourceRows();
        Assert.Equal(102, rows.Length);
        Assert.Equal(102, rows.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count());
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Equal(i + 1, rows[i].Row);
            Assert.True(OfficialUnitCodePolicy.TryNormalize(rows[i].Code, out var code));
            Assert.Equal(rows[i].Code, code);
            Assert.False(string.IsNullOrWhiteSpace(rows[i].Title));
        }
        Assert.Contains(rows, x => x.Code == "1627" && x.Title == "عدد");
        Assert.Contains(rows, x => x.Code == "164" && x.Title == "کیلوگرم");
        Assert.Contains(rows, x => x.Code == "1637" && x.Title == "لیتر");
        Assert.Contains(rows, x => x.Code == "16134" && x.Title == "نفر-ماه");
    }

    [Theory]
    [InlineData("1627", "1627")]
    [InlineData("۱۶۲۷", "1627")]
    [InlineData("١٦٢٧", "1627")]
    [InlineData(" 001627 ", "001627")]
    public void Unit_codes_are_normalized_without_numeric_rounding(string raw, string expected)
    {
        Assert.True(OfficialUnitCodePolicy.TryNormalize(raw, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcdefgh")]
    [InlineData("123456789")]
    [InlineData("16-27")]
    [InlineData("16 27")]
    [InlineData("１６２７")]
    public void Unknown_format_is_rejected(string raw)
    {
        Assert.False(OfficialUnitCodePolicy.TryNormalize(raw, out _));
    }

    [Fact]
    public void Item_conversion_is_exact_positive_and_period_bounded()
    {
        var start = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(OfficialUnitCodePolicy.ValidConversion(12, 1, start, null));
        Assert.True(OfficialUnitCodePolicy.ValidConversion(1, 24, start, start.AddDays(1)));
        Assert.False(OfficialUnitCodePolicy.ValidConversion(12, 0, start, null));
        Assert.False(OfficialUnitCodePolicy.ValidConversion(0, 1, start, null));
        Assert.False(OfficialUnitCodePolicy.ValidConversion(12, 1, start, start));
        Assert.False(OfficialUnitCodePolicy.ValidConversion(12, 1, start, start.AddSeconds(-1)));
        Assert.False(OfficialUnitCodePolicy.ValidConversion(12, 1, DateTime.SpecifyKind(start, DateTimeKind.Local), null));
    }
}
