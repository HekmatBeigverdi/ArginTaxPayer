using ArginTaxPayer.Domain.Parties;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class LegalEntityProfilePolicyTests
{
    [Theory]
    [InlineData("۱۰۱۰۰۰۰۰۰۰۰", "10100000000", "۱۲۳۴۵", "12345")]
    [InlineData("10100000000", "10100000000", "12345", "12345")]
    public void Legal_entity_normalizes_national_id_and_registration(
        string id, string normalizedId, string registration, string normalizedRegistration)
    {
        Assert.True(LegalEntityProfilePolicy.TryValidate(id, "  شرکت نمونه  ",
            registration, out var data));
        Assert.Equal(normalizedId, data!.NormalizedNationalIdentifier);
        Assert.Equal("شرکت نمونه", data.RegisteredName);
        Assert.Equal(normalizedRegistration, data.RegistrationNumber);
    }

    [Fact]
    public void Optional_registration_number_remains_null()
    {
        Assert.True(LegalEntityProfilePolicy.TryValidate("10100000000", "شرکت نمونه",
            null, out var data));
        Assert.Null(data!.RegistrationNumber);
    }

    [Theory]
    [InlineData("11111111111", "شرکت نمونه", "123")]
    [InlineData("1010000000", "شرکت نمونه", "123")]
    [InlineData("10100000000", " ", "123")]
    [InlineData("10100000000", "ش", "123")]
    [InlineData("10100000000", "شرکت\nنمونه", "123")]
    [InlineData("10100000000", "شرکت نمونه", "12-34")]
    [InlineData("10100000000", "شرکت نمونه", "12\n")]
    public void Rejects_invalid_fields(string id, string name, string registration)
        => Assert.False(LegalEntityProfilePolicy.TryValidate(id, name, registration, out _));
}
