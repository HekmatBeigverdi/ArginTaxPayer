using System.Text;
using ArginTaxPayer.Domain.Taxpayers;

namespace ArginTaxPayer.Domain.Parties;

/// <summary>Local input validation only. Does not verify corporate registration with a government authority.</summary>
public static class LegalEntityProfilePolicy
{
    public static bool TryValidate(string? nationalIdentifier, string? registeredName,
        string? registrationNumber, out LegalEntityProfileData? profile)
    {
        profile = null;
        if (!IranianTaxpayerIdentifier.TryNormalize(TaxpayerKind.LegalEntity, nationalIdentifier,
                out var normalizedIdentifier) || !TryName(registeredName, out var name) ||
            !TryRegistrationNumber(registrationNumber, out var registration))
            return false;
        profile = new LegalEntityProfileData(normalizedIdentifier, name, registration);
        return true;
    }

    private static bool TryName(string? raw, out string name)
    {
        name = string.Empty;
        if (raw is null || raw.Length > 512 || raw.Any(char.IsControl)) return false;
        var candidate = raw.Trim().Normalize(NormalizationForm.FormC);
        if (candidate.Length is < 2 or > 256 || !candidate.Any(c => !char.IsWhiteSpace(c)))
            return false;
        name = candidate;
        return true;
    }

    private static bool TryRegistrationNumber(string? raw, out string? registration)
    {
        registration = null;
        if (raw is null || raw.Length == 0) return true;
        if (raw.Length > 40 || raw.Any(char.IsControl)) return false;
        var value = raw.Trim();
        if (value.Length is < 1 or > 32) return false;
        var normalized = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is >= '0' and <= '9') normalized.Append(c);
            else if (c is >= '۰' and <= '۹') normalized.Append((char)('0' + c - '۰'));
            else if (c is >= '٠' and <= '٩') normalized.Append((char)('0' + c - '٠'));
            else return false;
        }
        registration = normalized.ToString();
        return true;
    }
}

public sealed record LegalEntityProfileData(
    string NormalizedNationalIdentifier, string RegisteredName, string? RegistrationNumber);
