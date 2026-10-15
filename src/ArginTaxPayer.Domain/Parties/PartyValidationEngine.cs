using System.Text;
using ArginTaxPayer.Domain.Taxpayers;

namespace ArginTaxPayer.Domain.Parties;

/// <summary>
/// Versioned local syntactic rule set. Does not perform authoritative identity/registry verification.
/// Stable error codes can be mapped to localized API messages without exposing input values.
/// </summary>
public static class PartyValidationEngine
{
    public const string RuleVersion = "phase05.party.v1";

    public static PartyValidationResult ValidateNatural(string? nationalCode, string? firstName, string? lastName)
    {
        var issues = new List<PartyValidationIssue>();
        var identifier = ValidateIdentifier(TaxpayerKind.NaturalPerson, nationalCode, "nationalCode", issues);
        var first = ValidateName(firstName, "firstName", 1, 100, issues);
        var last = ValidateName(lastName, "lastName", 1, 100, issues);
        return issues.Count == 0
            ? new(RuleVersion, Array.Empty<PartyValidationIssue>(),
                new NaturalPersonProfileData(identifier!, first!, last!), null)
            : new(RuleVersion, issues.ToArray(), null, null);
    }

    public static PartyValidationResult ValidateLegal(string? nationalIdentifier, string? registeredName,
        string? registrationNumber)
    {
        var issues = new List<PartyValidationIssue>();
        var identifier = ValidateIdentifier(TaxpayerKind.LegalEntity, nationalIdentifier, "nationalIdentifier", issues);
        var name = ValidateName(registeredName, "registeredName", 2, 256, issues);
        var registration = ValidateRegistrationNumber(registrationNumber, issues);
        return issues.Count == 0
            ? new(RuleVersion, Array.Empty<PartyValidationIssue>(), null,
                new LegalEntityProfileData(identifier!, name!, registration))
            : new(RuleVersion, issues.ToArray(), null, null);
    }

    private static string? ValidateIdentifier(TaxpayerKind kind, string? raw,
        string field, List<PartyValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            issues.Add(new(field, PartyValidationCodes.Required));
            return null;
        }

        // The Phase04 normalizer is the single source of truth for digit conversion
        // and national-code checksum; do not introduce a competing checksum implementation.
        if (raw.Any(char.IsControl) ||
            !IranianTaxpayerIdentifier.TryNormalize(kind, raw, out var normalized))
        {
            issues.Add(new(field, PartyValidationCodes.InvalidIdentifier));
            return null;
        }
        return normalized;
    }

    private static string? ValidateName(string? raw, string field,
        int minLength, int maxLength, List<PartyValidationIssue> issues)
    {
        if (raw is null || raw.Length == 0 || string.IsNullOrWhiteSpace(raw))
        {
            issues.Add(new(field, PartyValidationCodes.Required));
            return null;
        }
        // Reject controls *before* Trim; preserves the owner's Step03 Codex fix.
        if (raw.Any(char.IsControl))
        {
            issues.Add(new(field, PartyValidationCodes.ControlCharacter));
            return null;
        }
        if (raw.Length > 512)
        {
            issues.Add(new(field, PartyValidationCodes.InvalidLength));
            return null;
        }
        var name = raw.Trim().Normalize(NormalizationForm.FormC);
        if (name.Length < minLength || name.Length > maxLength)
        {
            issues.Add(new(field, PartyValidationCodes.InvalidLength));
            return null;
        }
        return name;
    }

    private static string? ValidateRegistrationNumber(string? raw, List<PartyValidationIssue> issues)
    {
        if (raw is null || raw.Length == 0) return null;
        if (raw.Any(char.IsControl))
        {
            issues.Add(new("registrationNumber", PartyValidationCodes.ControlCharacter));
            return null;
        }
        if (raw.Length > 40)
        {
            issues.Add(new("registrationNumber", PartyValidationCodes.InvalidLength));
            return null;
        }
        var value = raw.Trim();
        if (value.Length is < 1 or > 32)
        {
            issues.Add(new("registrationNumber", PartyValidationCodes.InvalidLength));
            return null;
        }
        var normalized = new StringBuilder(value.Length);
        foreach (var digit in value)
        {
            if (digit is >= '0' and <= '9') normalized.Append(digit);
            else if (digit is >= '۰' and <= '۹') normalized.Append((char)('0' + digit - '۰'));
            else if (digit is >= '٠' and <= '٩') normalized.Append((char)('0' + digit - '٠'));
            else
            {
                issues.Add(new("registrationNumber", PartyValidationCodes.DigitsOnly));
                return null;
            }
        }
        return normalized.ToString();
    }
}

public static class PartyValidationCodes
{
    public const string Required = "party.validation.required";
    public const string InvalidIdentifier = "party.validation.identifier_invalid";
    public const string InvalidLength = "party.validation.length_invalid";
    public const string ControlCharacter = "party.validation.control_character";
    public const string DigitsOnly = "party.validation.digits_only";
}

public sealed record PartyValidationIssue(string Field, string Code);

public sealed record PartyValidationResult(
    string Version,
    IReadOnlyList<PartyValidationIssue> Issues,
    NaturalPersonProfileData? NaturalPerson,
    LegalEntityProfileData? LegalEntity)
{
    public bool IsValid => Issues.Count == 0;
}
