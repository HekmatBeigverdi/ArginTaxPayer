using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08Step09FormMatrixTests
{
    [Fact]
    public void V79Metadata_has_only_official_pairs_and_conditional_requirements()
    {
        var matrix = OfficialV79InvoicePatternMatrix.Create();
        Assert.Equal("RC_IITP.IS_V7.9", matrix.Source.Edition);
        Assert.False(matrix.Source.VerifiedForSubmission);
        Assert.Equal(17, matrix.Entries.Count);
        Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
            matrix.Evaluate(new InvoiceTypePattern(ElectronicInvoiceType.First,4),
                new DateTime(2026,10,19,0,0,0,DateTimeKind.Utc)));
        Assert.Equal(InvoicePatternDecision.UnsupportedCombination,
            matrix.Evaluate(new InvoiceTypePattern(ElectronicInvoiceType.Second,4),
                new DateTime(2026,10,19,0,0,0,DateTimeKind.Utc)));
        Assert.Equal(InvoicePatternDecision.AllowedByReferencedEdition,
            matrix.Evaluate(new InvoiceTypePattern(ElectronicInvoiceType.Third,null),
                new DateTime(2026,10,19,0,0,0,DateTimeKind.Utc)));
    }
}
