using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoicePatternsTests
{
    private static readonly DateTime ValidDate =
        new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Notice_six_has_exactly_ten_historical_type_pattern_combinations()
    {
        var matrix = HistoricalInvoicePatternMatrix.Create();
        Assert.Equal(10, matrix.Entries.Count);
        foreach (var number in Enumerable.Range(1, 7))
            Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
                matrix.Evaluate(new(ElectronicInvoiceType.First, number), ValidDate));
        foreach (var number in Enumerable.Range(1, 2))
            Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
                matrix.Evaluate(new(ElectronicInvoiceType.Second, number), ValidDate));
        Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
            matrix.Evaluate(new(ElectronicInvoiceType.Third, null), ValidDate));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 8)]
    [InlineData(2, 3)]
    [InlineData(2, 7)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    public void Undocumented_pairs_are_rejected(int type, int pattern)
    {
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            HistoricalInvoicePatternMatrix.Create()
                .Evaluate(new((ElectronicInvoiceType)type, pattern), ValidDate));
    }

    [Fact]
    public void Type_three_is_patternless_and_others_require_pattern()
    {
        var matrix = HistoricalInvoicePatternMatrix.Create();
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            matrix.Evaluate(new(ElectronicInvoiceType.First, null), ValidDate));
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            matrix.Evaluate(new(ElectronicInvoiceType.Second, null), ValidDate));
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            matrix.Evaluate(new(ElectronicInvoiceType.Third, 0), ValidDate));
    }

    [Fact]
    public void Historical_notice_never_claims_verified_submission()
    {
        Assert.Equal(InvoicePatternDecision.UnverifiedForSubmission,
            HistoricalInvoicePatternMatrix.Create().Evaluate(
                new(ElectronicInvoiceType.First, 1), ValidDate, true));
    }

    [Fact]
    public void Version_is_explicit_and_missing_version_fails_closed()
    {
        var registry = new InvoicePatternRegistry(new[] { HistoricalInvoicePatternMatrix.Create() });
        var pair = new InvoiceTypePattern(ElectronicInvoiceType.First, 1);
        Assert.Equal(InvoicePatternDecision.MissingEdition,
            registry.Evaluate("unregistered", pair, ValidDate));
        Assert.Equal(InvoicePatternDecision.MissingEdition,
            registry.Evaluate(null, pair, ValidDate));
        Assert.Throws<ArgumentException>(() => new InvoicePatternRegistry(new[]
            { HistoricalInvoicePatternMatrix.Create(), HistoricalInvoicePatternMatrix.Create() }));
    }

    [Fact]
    public void Date_window_is_utc_and_half_open()
    {
        var matrix = HistoricalInvoicePatternMatrix.Create();
        var pair = new InvoiceTypePattern(ElectronicInvoiceType.First, 1);
        Assert.Equal(InvoicePatternDecision.OutsideEffectiveWindow,
            matrix.Evaluate(pair, new DateTime(2023, 3, 20, 0, 0, 0, DateTimeKind.Utc)));
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
        var src = HistoricalInvoicePatternMatrix.Create().Source;
        Assert.Throws<ArgumentException>(() => new InvoicePatternMatrix(src,
            new[] { new InvoiceTypePattern(ElectronicInvoiceType.Third, 1) }));
        Assert.Throws<ArgumentException>(() => new InvoicePatternMatrix(src,
            new[] { new InvoiceTypePattern(ElectronicInvoiceType.First, null) }));
        var entries = HistoricalInvoicePatternMatrix.Create().Entries;
        Assert.False(entries is InvoiceTypePattern[]);
    }
}
