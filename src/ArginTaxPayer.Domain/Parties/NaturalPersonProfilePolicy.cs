namespace ArginTaxPayer.Domain.Parties;

/// <summary>Compatibility facade: existing Step03 callers now use the shared versioned rules.</summary>
public static class NaturalPersonProfilePolicy
{
    public static bool TryValidate(string? nationalCode, string? firstName,
        string? lastName, out NaturalPersonProfileData? profile)
    {
        var result = PartyValidationEngine.ValidateNatural(nationalCode, firstName, lastName);
        profile = result.NaturalPerson;
        return result.IsValid;
    }
}

public sealed record NaturalPersonProfileData(
    string NormalizedNationalCode, string FirstName, string LastName)
{
    public string DisplayName => FirstName + " " + LastName;
}
