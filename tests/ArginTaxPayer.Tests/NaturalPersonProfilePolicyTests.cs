using ArginTaxPayer.Domain.Parties;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class NaturalPersonProfilePolicyTests
{
    [Theory]
    [InlineData("۰۰۱۲۳۴۵۶۷۹", "0012345679")]
    [InlineData("0012345679", "0012345679")]
    [InlineData("٠٠١٢٣٤٥٦٧٩", "0012345679")]
    public void Accepts_valid_national_code_and_normalizes_digits(string code, string expected)
    {
        Assert.True(NaturalPersonProfilePolicy.TryValidate(code, "  علی  ", " رضایی ", out var data));
        Assert.NotNull(data);
        Assert.Equal(expected, data!.NormalizedNationalCode);
        Assert.Equal("علی رضایی", data.DisplayName);
    }

    [Theory]
    [InlineData("0012345678", "علی", "رضایی")]
    [InlineData("0000000000", "علی", "رضایی")]
    [InlineData("123456789", "علی", "رضایی")]
    [InlineData("12345-6789", "علی", "رضایی")]
    [InlineData("0012345679", " ", "رضایی")]
    [InlineData("0012345679", "علی", "")]
    [InlineData("0012345679", "علی\n", "رضایی")]
    public void Rejects_invalid_national_code_or_name(string code, string firstName, string lastName)
        => Assert.False(NaturalPersonProfilePolicy.TryValidate(code, firstName, lastName, out _));

    [Fact]
    public void Rejects_overlong_name()
        => Assert.False(NaturalPersonProfilePolicy.TryValidate(
            "0012345679", new string('A', 101), "Test", out _));
}
