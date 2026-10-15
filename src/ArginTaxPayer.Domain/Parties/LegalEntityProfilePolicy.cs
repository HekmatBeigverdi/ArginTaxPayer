namespace ArginTaxPayer.Domain.Parties;

/// <summary>Compatibility facade: existing Step04 callers now use the shared versioned rules.</summary>
public static class LegalEntityProfilePolicy
{
    public static bool TryValidate(string? nationalIdentifier, string? registeredName,
        string? registrationNumber, out LegalEntityProfileData? profile)
    {
        var result = PartyValidationEngine.ValidateLegal(nationalIdentifier, registeredName, registrationNumber);
        profile = result.LegalEntity;
        return result.IsValid;
    }
}

public sealed record LegalEntityProfileData(
    string NormalizedNationalIdentifier, string RegisteredName, string? RegistrationNumber);
