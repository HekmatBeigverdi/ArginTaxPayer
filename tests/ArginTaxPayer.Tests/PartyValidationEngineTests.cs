using ArginTaxPayer.Domain.Parties;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PartyValidationEngineTests
{
    [Fact]
    public void Natural_golden_fixture_normalizes_and_reports_version()
    {
        var result = PartyValidationEngine.ValidateNatural("۰۰۱۲۳۴۵۶۷۹", "  علی ", " رضایی ");
        Assert.True(result.IsValid);
        Assert.Equal(PartyValidationEngine.RuleVersion, result.Version);
        Assert.Empty(result.Issues);
        Assert.Equal("0012345679", result.NaturalPerson!.NormalizedNationalCode);
        Assert.Equal("علی رضایی", result.NaturalPerson.DisplayName);
        Assert.Null(result.LegalEntity);
    }

    [Fact]
    public void Legal_golden_fixture_normalizes_identifiers()
    {
        var result = PartyValidationEngine.ValidateLegal("۱۰۱۰۰۰۰۰۰۰۰", " شرکت نمونه ", "۱۲۳۴");
        Assert.True(result.IsValid);
        Assert.Equal("10100000000", result.LegalEntity!.NormalizedNationalIdentifier);
        Assert.Equal("1234", result.LegalEntity.RegistrationNumber);
        Assert.Null(result.NaturalPerson);
    }

    [Fact]
    public void Reports_multiple_stable_errors_without_echoing_personal_data()
    {
        var result = PartyValidationEngine.ValidateNatural("0012345678", "علی\n", "");
        Assert.False(result.IsValid);
        Assert.Null(result.NaturalPerson);
        Assert.Equal(new[]
        {
            new PartyValidationIssue("nationalCode", PartyValidationCodes.InvalidIdentifier),
            new PartyValidationIssue("firstName", PartyValidationCodes.ControlCharacter),
            new PartyValidationIssue("lastName", PartyValidationCodes.Required)
        }, result.Issues);
        Assert.DoesNotContain("0012345678", string.Join(" ", result.Issues.Select(e => e.Code)));
    }

    [Theory]
    [InlineData("12-34", PartyValidationCodes.DigitsOnly)]
    [InlineData("   ", PartyValidationCodes.InvalidLength)]
    [InlineData("123\n", PartyValidationCodes.ControlCharacter)]
    public void Legal_registration_negative_fixtures(string registration, string code)
    {
        var result = PartyValidationEngine.ValidateLegal("10100000000", "شرکت نمونه", registration);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, e => e.Field == "registrationNumber" && e.Code == code);
    }

    [Fact]
    public void Existing_facades_return_same_result_as_shared_engine()
    {
        var a = PartyValidationEngine.ValidateNatural("0012345679", "علی", "رضایی");
        Assert.True(NaturalPersonProfilePolicy.TryValidate("0012345679", "علی", "رضایی", out var n));
        Assert.Equal(a.NaturalPerson, n);
        var b = PartyValidationEngine.ValidateLegal("10100000000", "شرکت نمونه", null);
        Assert.True(LegalEntityProfilePolicy.TryValidate("10100000000", "شرکت نمونه", null, out var l));
        Assert.Equal(b.LegalEntity, l);
    }

    [Fact]
    public void Invalid_legal_identifier_does_not_claim_official_verification()
    {
        var bad = PartyValidationEngine.ValidateLegal("11111111111", "شرکت نمونه", null);
        Assert.Contains(bad.Issues, x => x.Code == PartyValidationCodes.InvalidIdentifier);
    }
}
