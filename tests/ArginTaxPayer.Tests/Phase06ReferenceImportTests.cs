using System.Text;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06ReferenceImportTests
{
    private const string Source = "https://example.invalid/official-source-reviewed-by-operator";
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void Valid_unit_file_has_deterministic_fingerprint_and_preview()
    {
        var data = Bytes("1627|عدد\n1637|لیتر\n");
        var a = ReferenceImportParser.Preview(ReferenceImportKind.Units, "RC_UMGS.ST", "V1.18", Source, data);
        var b = ReferenceImportParser.Preview(ReferenceImportKind.Units, "RC_UMGS.ST", "V1.18", Source, data);
        Assert.True(a.IsValid);
        Assert.Equal(2, a.Rows.Count);
        Assert.Equal(a.Sha256, b.Sha256);
        Assert.Equal(64, a.Sha256.Length);
        Assert.Equal("1627", a.Rows[0].Code);
        Assert.Equal("عدد", a.Rows[0].Title);
        Assert.Empty(a.Issues);
    }

    [Fact]
    public void Goods_and_services_are_never_marked_verified_during_preview()
    {
        var a = ReferenceImportParser.Preview(
            ReferenceImportKind.GoodsServices, "RC_GS", "candidate",
            Source, Bytes("1234567890123|خدمت نصب|Service\n1234567890124|کالای تست|Goods\n"));
        Assert.True(a.IsValid);
        Assert.Equal(new[] { 2, 1 }, a.Rows.Select(x => x.Kind));
    }

    [Theory]
    [InlineData("1627|عدد\n1627|دانه\n", "duplicate_code")]
    [InlineData("1627|عدد|extra\n", "invalid_columns")]
    [InlineData("ABC|عدد\n", "invalid_code_or_title")]
    [InlineData("1627|a\n", "invalid_code_or_title")]
    [InlineData("", "invalid_file_size")]
    public void Invalid_unit_file_returns_row_level_diagnostics(string value, string reason)
    {
        var a = ReferenceImportParser.Preview(ReferenceImportKind.Units, "RC_UMGS.ST", "V1.18",
            Source, Bytes(value));
        Assert.False(a.IsValid);
        Assert.Contains(a.Issues, issue => issue.Message == reason);
    }

    [Fact]
    public void Same_number_is_distinct_in_goods_and_services_but_not_twice_within_same_kind()
    {
        var a = ReferenceImportParser.Preview(ReferenceImportKind.GoodsServices,
            "RC_GS", "draft", Source,
            Bytes("1234567890123|کالای تست|Goods\n1234567890123|خدمت تست|Service\n"));
        Assert.True(a.IsValid);
        var b = ReferenceImportParser.Preview(ReferenceImportKind.GoodsServices,
            "RC_GS", "draft", Source,
            Bytes("1234567890123|خدمت تست|Service\n1234567890123|خدمت دیگر|Service\n"));
        Assert.False(b.IsValid);
        Assert.Contains(b.Issues, x => x.Message == "duplicate_code" && x.Line == 2);
    }

    [Fact]
    public void Rejects_invalid_utf8_untrusted_origin_and_oversized_payload()
    {
        var bad = ReferenceImportParser.Preview(ReferenceImportKind.Units, "RC_UMGS.ST", "V1.18",
            Source, new byte[] { 0xFF, 0xFE });
        Assert.Contains(bad.Issues, x => x.Message == "invalid_utf8");
        var wrongSource = ReferenceImportParser.Preview(ReferenceImportKind.Units, "RC_UMGS.ST",
            "V1.18", "http://unsafe.example/test", Bytes("1627|عدد\n"));
        Assert.Contains(wrongSource.Issues, x => x.Message == "invalid_document_edition_or_https_source");
        var tooBig = ReferenceImportParser.Preview(ReferenceImportKind.Units, "RC_UMGS.ST",
            "V1.18", Source, new byte[ReferenceImportParser.MaxBytes + 1]);
        Assert.Contains(tooBig.Issues, x => x.Message == "invalid_file_size");
    }

    [Fact]
    public void Rejects_truncated_preview_and_too_many_rows()
    {
        var data = Bytes(string.Concat(
            Enumerable.Repeat("1627|عدد\n", ReferenceImportParser.MaxRows + 1)));
        var result = ReferenceImportParser.Preview(ReferenceImportKind.Units,
            "RC_UMGS.ST", "test", Source, data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Message == "too_many_rows");
    }
}
