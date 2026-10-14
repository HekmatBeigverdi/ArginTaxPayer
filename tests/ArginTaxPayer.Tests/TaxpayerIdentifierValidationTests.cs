using ArginTaxPayer.Domain.Taxpayers;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class TaxpayerIdentifierValidationTests
{
    [Theory]
    [InlineData("۰۰۱۲۳۴۵۶۷۹", "0012345679")]
    [InlineData("0012345679", "0012345679")]
    public void Natural_person_accepts_valid_check_digit_and_normalizes(string input, string expected)
    {
        Assert.True(IranianTaxpayerIdentifier.TryNormalize(TaxpayerKind.NaturalPerson, input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("0012345678")]
    [InlineData("0000000000")]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("12345-6789")]
    public void Natural_person_rejects_invalid_identifiers(string input)
        => Assert.False(IranianTaxpayerIdentifier.TryNormalize(TaxpayerKind.NaturalPerson, input, out _));

    [Fact]
    public void Legal_entity_requires_eleven_digits_without_fabricating_official_verification()
    {
        Assert.True(IranianTaxpayerIdentifier.TryNormalize(TaxpayerKind.LegalEntity, "۱۰۱۰۰۰۰۰۰۰۰", out var normalized));
        Assert.Equal("10100000000", normalized);
        Assert.False(IranianTaxpayerIdentifier.TryNormalize(TaxpayerKind.LegalEntity, "1234567890", out _));
        Assert.False(IranianTaxpayerIdentifier.TryNormalize(TaxpayerKind.LegalEntity, "11111111111", out _));
    }

    [Fact]
    public void Unknown_kind_is_rejected()
        => Assert.False(IranianTaxpayerIdentifier.TryNormalize((TaxpayerKind)99, "0012345679", out _));
}
