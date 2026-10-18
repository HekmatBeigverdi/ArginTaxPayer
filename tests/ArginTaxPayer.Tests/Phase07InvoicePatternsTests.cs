using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoicePatternsTests
{
    private static readonly DateTime ValidDate =
        new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Official_V79_has_seventeen_type_pattern_combinations()
    {
        var matrix = OfficialV79InvoicePatternMatrix.Create();
        Assert.Equal(17, matrix.Entries.Count);
        foreach (var number in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 13, 14 })
            Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
                matrix.Evaluate(new(ElectronicInvoiceType.First, number), ValidDate));
        foreach (var number in new[] { 1, 3, 9, 13 })
            Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
                matrix.Evaluate(new(ElectronicInvoiceType.Second, number), ValidDate));
        Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
            matrix.Evaluate(new(ElectronicInvoiceType.Third, null), ValidDate));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 10)]
    [InlineData(2, 2)]
    [InlineData(2, 7)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    public void Undocumented_pairs_are_rejected(int type, int pattern)
    {
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            OfficialV79InvoicePatternMatrix.Create()
                .Evaluate(new((ElectronicInvoiceType)type, pattern), ValidDate));
    }

    [Fact]
    public void Type_three_is_patternless_and_others_require_pattern()
    {
        var matrix = OfficialV79InvoicePatternMatrix.Create();
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            matrix.Evaluate(new(ElectronicInvoiceType.First, null), ValidDate));
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            matrix.Evaluate(new(ElectronicInvoiceType.Second, null), ValidDate));
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            matrix.Evaluate(new(ElectronicInvoiceType.Third, 0), ValidDate));
    }

    [Fact]
    public void Official_document_does_not_imply_approved_submission()
    {
        Assert.Equal(InvoicePatternDecision.UnverifiedForSubmission,
            OfficialV79InvoicePatternMatrix.Create().Evaluate(
                new(ElectronicInvoiceType.First, 1), ValidDate, true));
    }

    [Fact]
    public void Version_is_explicit_and_missing_version_fails_closed()
    {
        var registry = new InvoicePatternRegistry(new[] { OfficialV79InvoicePatternMatrix.Create() });
        var pair = new InvoiceTypePattern(ElectronicInvoiceType.First, 1);
        Assert.Equal(InvoicePatternDecision.MissingEdition,
            registry.Evaluate("unregistered", pair, ValidDate));
        Assert.Equal(InvoicePatternDecision.MissingEdition,
            registry.Evaluate(null, pair, ValidDate));
        Assert.Throws<ArgumentException>(() => new InvoicePatternRegistry(new[]
            { OfficialV79InvoicePatternMatrix.Create(), OfficialV79InvoicePatternMatrix.Create() }));
    }

    [Fact]
    public void Date_window_is_utc_and_half_open()
    {
        var matrix = OfficialV79InvoicePatternMatrix.Create();
        var pair = new InvoiceTypePattern(ElectronicInvoiceType.First, 1);
        Assert.Equal(InvoicePatternDecision.OutsideEffectiveWindow,
            matrix.Evaluate(pair, DateTime.SpecifyKind(ValidDate, DateTimeKind.Unspecified)));
        Assert.Equal(InvoicePatternDecision.OutsideEffectiveWindow,
            matrix.Evaluate(pair, DateTime.SpecifyKind(ValidDate, DateTimeKind.Local)));
        var source = new InvoicePatternSource("doc", "test", "source",
            ValidDate, ValidDate.AddDays(1), true);
        var bounded = new InvoicePatternMatrix(source, new[] { pair });
        Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
            bounded.Evaluate(pair, ValidDate, true));
        Assert.Equal(InvoicePatternDecision.OutsideEffectiveWindow,
            bounded.Evaluate(pair, ValidDate.AddDays(1), true));
    }

    [Fact]
    public void Invalid_matrices_are_rejected_and_public_entries_cannot_mutate_rules()
    {
        var src = OfficialV79InvoicePatternMatrix.Create().Source;
        Assert.Throws<ArgumentException>(() => new InvoicePatternMatrix(src,
            new[] { new InvoiceTypePattern(ElectronicInvoiceType.Third, 1) }));
        Assert.Throws<ArgumentException>(() => new InvoicePatternMatrix(src,
            new[] { new InvoiceTypePattern(ElectronicInvoiceType.First, null) }));
        var entries = OfficialV79InvoicePatternMatrix.Create().Entries;
        Assert.False(entries is InvoiceTypePattern[]);
    }
}
