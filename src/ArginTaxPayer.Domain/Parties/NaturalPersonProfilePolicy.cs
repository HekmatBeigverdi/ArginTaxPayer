using System.Text;
using ArginTaxPayer.Domain.Taxpayers;

namespace ArginTaxPayer.Domain.Parties;

/// <summary>
/// Local syntactic validation for an Iranian natural person.
/// This does not perform any official identity lookup or ownership authorization.
/// </summary>
public static class NaturalPersonProfilePolicy
{
    public static bool TryValidate(
        string? nationalCode,
        string? firstName,
        string? lastName,
        out NaturalPersonProfileData? profile)
    {
        profile = null;
        if (!IranianTaxpayerIdentifier.TryNormalize(TaxpayerKind.NaturalPerson, nationalCode, out var code) ||
            !TryNormalizeName(firstName, out var first) ||
            !TryNormalizeName(lastName, out var last))
            return false;

        profile = new NaturalPersonProfileData(code, first, last);
        return true;
    }

    private static bool TryNormalizeName(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (input is null || input.Length > 512) return false;
        var value = input.Trim().Normalize(NormalizationForm.FormC);
        if (value.Length is < 1 or > 100 || value.Any(char.IsControl)) return false;
        // Reject whitespace-only inputs; do not impose a language/character-set assumption.
        if (!value.Any(c => !char.IsWhiteSpace(c))) return false;
        normalized = value;
        return true;
    }
}

public sealed record NaturalPersonProfileData(
    string NormalizedNationalCode,
    string FirstName,
    string LastName)
{
    public string DisplayName => FirstName + " " + LastName;
}
