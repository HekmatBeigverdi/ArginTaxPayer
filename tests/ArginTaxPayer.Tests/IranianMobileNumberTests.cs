using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IranianMobileNumberTests
{
    [Theory]
    [InlineData("09123456789")]
    [InlineData("9123456789")]
    [InlineData("989123456789")]
    [InlineData("+989123456789")]
    [InlineData("00989123456789")]
    [InlineData("۰۹۱۲۳۴۵۶۷۸۹")]
    [InlineData("٠٩١٢٣٤٥٦٧٨٩")]
    [InlineData("0912 345 6789")]
    [InlineData("0912-345-6789")]
    public void AcceptsCanonicalAndCommonIranianMobileInput(string input)
    {
        Assert.True(IranianMobileNumber.TryNormalize(input, out var mobile));
        Assert.Equal("+989123456789", mobile);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("02112345678")]
    [InlineData("0912345678")]
    [InlineData("091234567890")]
    [InlineData("+14155552671")]
    [InlineData("0098912345abc")]
    [InlineData("0912/3456789")]
    [InlineData("++989123456789")]
    public void RejectsNonMobileOrMalformedInput(string? input)
    {
        Assert.False(IranianMobileNumber.TryNormalize(input, out var mobile));
        Assert.Equal(string.Empty, mobile);
    }
}
