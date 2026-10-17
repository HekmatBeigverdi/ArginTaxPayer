using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07OfficialV79Tests
{
    private static readonly DateTime Date = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Official_V79_has_twelve_type_one_and_four_type_two_patterns()
    {
        var matrix = OfficialV79InvoicePatternMatrix.Create();
        Assert.Equal(17, matrix.Entries.Count);
        foreach (var pattern in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 13, 14 })
            Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
                matrix.Evaluate(new(ElectronicInvoiceType.First, pattern), Date));
        foreach (var pattern in new[] { 1, 3, 9, 13 })
            Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
                matrix.Evaluate(new(ElectronicInvoiceType.Second, pattern), Date));
        Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
            matrix.Evaluate(new(ElectronicInvoiceType.Third, null), Date));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(1, 12)]
    [InlineData(2, 2)]
    [InlineData(2, 7)]
    [InlineData(3, 1)]
    public void V79_rejects_other_type_pattern_combinations(int type, int pattern)
    {
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            OfficialV79InvoicePatternMatrix.Create()
                .Evaluate(new((ElectronicInvoiceType)type, pattern), Date));
    }

    [Fact]
    public void V79_does_not_imply_verified_current_submission_eligibility()
    {
        var matrix = OfficialV79InvoicePatternMatrix.Create();
        Assert.Equal(OfficialV79InvoicePatternMatrix.Edition, matrix.Source.Edition);
        Assert.Equal(InvoicePatternDecision.UnverifiedForSubmission,
            matrix.Evaluate(new(ElectronicInvoiceType.First, 14), Date, true));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(11)]
    public void V79_export_waybill_and_commodity_securities_disallow_sales_return(int pattern)
    {
        var selection = new InvoiceTypePattern(ElectronicInvoiceType.First, pattern);
        Assert.False(OfficialV79InvoiceSubjectPolicy.Allows(selection, InvoiceSubject.SalesReturn));
        Assert.True(OfficialV79InvoiceSubjectPolicy.Allows(selection, InvoiceSubject.Original));
        Assert.True(OfficialV79InvoiceSubjectPolicy.Allows(selection, InvoiceSubject.Correction));
        Assert.True(OfficialV79InvoiceSubjectPolicy.Allows(selection, InvoiceSubject.Cancellation));
    }

    [Fact]
    public void Unknown_pattern_and_subject_are_never_approved()
    {
        Assert.False(OfficialV79InvoiceSubjectPolicy.Allows(
            new(ElectronicInvoiceType.First, 12), InvoiceSubject.Original));
        Assert.False(OfficialV79InvoiceSubjectPolicy.Allows(
            new(ElectronicInvoiceType.First, 1), (InvoiceSubject)99));
    }
}
