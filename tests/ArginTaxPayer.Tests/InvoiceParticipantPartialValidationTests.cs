using System.Reflection;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class InvoiceParticipantPartialValidationTests
{
    private static InvoiceParticipantWrite? Clean(InvoiceParticipantWrite raw) =>
        (InvoiceParticipantWrite?)typeof(InvoiceParticipantDetailsService)
            .GetMethod("Clean", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object?[] { raw });

    [Fact]
    public void Province_only_may_be_saved_without_any_other_fiscal_field()
    {
        var clean = Clean(new InvoiceParticipantWrite(null, null, null,
            " تهران ", null, null, null, null, null, null, 0));
        Assert.NotNull(clean);
        Assert.Equal("تهران", clean!.Province);
        Assert.Null(clean.EconomicCode);
        Assert.Null(clean.PostalCode);
        Assert.Equal(0, clean.ExpectedRevision);
    }

    [Fact]
    public void City_only_can_be_updated_independently()
    {
        var clean = Clean(new InvoiceParticipantWrite(null, null, null,
            null, " شیراز ", null, null, null, null, null, 9));
        Assert.NotNull(clean);
        Assert.Equal("شیراز", clean!.City);
        Assert.Null(clean.Address);
        Assert.Equal(9, clean.ExpectedRevision);
    }

    [Theory]
    [InlineData("۱۲۳۴۵۶۷۸۹۰", "1234567890")]
    [InlineData("١٢٣٤٥٦٧٨٩٠", "1234567890")]
    public void Persian_and_Arabic_postal_digits_are_normalized(string input, string expected)
    {
        var clean = Clean(new InvoiceParticipantWrite(null, null, input,
            null, null, null, null, null, null, null, 0));
        Assert.Equal(expected, clean!.PostalCode);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789A")]
    public void Invalid_populated_postal_code_is_rejected(string postal)
    {
        var clean = Clean(new InvoiceParticipantWrite(null, null, postal,
            null, null, null, null, null, null, null, 0));
        Assert.Null(clean);
    }

    [Fact]
    public void Null_and_empty_optional_values_do_not_block_save()
    {
        var clean = Clean(new InvoiceParticipantWrite(null, "  ", null,
            null, null, null, null, null, null, null, 0));
        Assert.NotNull(clean);
        Assert.Null(clean!.BranchCode);
    }
}
