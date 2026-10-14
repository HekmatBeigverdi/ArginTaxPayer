using System.Globalization;
using System.Net.Mail;
using System.Text;

namespace ArginTaxPayer.Domain.Parties;

public static class PartyContactAddressValidation
{
    public static bool TryContact(int kind, string? rawValue, string? rawContactPerson,
        out string value, out string? contactPerson)
    {
        value = string.Empty;
        contactPerson = null;
        if (rawValue is null || rawValue.Length > 320 || rawValue.Any(char.IsControl)) return false;
        var cleaned = rawValue.Trim().Normalize(NormalizationForm.FormC);
        if (kind is 1 or 2)
        {
            var digits = new StringBuilder();
            foreach (var c in cleaned)
            {
                if (c is >= '0' and <= '9') digits.Append(c);
                else if (c is >= '۰' and <= '۹') digits.Append((char)('0' + c - '۰'));
                else if (c is >= '٠' and <= '٩') digits.Append((char)('0' + c - '٠'));
                else if (c == '+' && digits.Length == 0) digits.Append(c);
                else return false;
            }
            value = digits.ToString();
            if (kind == 1 && !(value.Length == 11 && value.StartsWith("09", StringComparison.Ordinal) ||
                value.Length == 13 && value.StartsWith("+989", StringComparison.Ordinal))) return false;
            if (kind == 2 && (value.TrimStart('+').Length is < 7 or > 15)) return false;
        }
        else if (kind == 3)
        {
            if (cleaned.Length is < 3 or > 320 || cleaned.Any(char.IsWhiteSpace)) return false;
            try
            {
                var parsed = new MailAddress(cleaned);
                if (!string.Equals(parsed.Address, cleaned, StringComparison.Ordinal) ||
                    !parsed.Host.Contains('.', StringComparison.Ordinal)) return false;
            }
            catch (FormatException) { return false; }
            value = cleaned.ToLowerInvariant();
        }
        else return false;

        if (rawContactPerson is not null)
        {
            if (!TryText(rawContactPerson, 1, 120, out var person)) return false;
            contactPerson = person;
        }
        return true;
    }

    public static bool TryAddress(string? label, string? countryCode, string? province,
        string? city, string? street, string? postalCode, out NormalizedPartyAddress? result)
    {
        result = null;
        if (!TryText(label, 1, 80, out var l) || !TryText(province, 1, 120, out var p) ||
            !TryText(city, 1, 120, out var c) || !TryText(street, 3, 500, out var s))
            return false;
        if (countryCode is null || countryCode.Length != 2 ||
            !countryCode.All(x => x is >= 'a' and <= 'z' or >= 'A' and <= 'Z')) return false;
        string? postal = null;
        if (postalCode is not null)
        {
            if (postalCode.Any(char.IsControl) || postalCode.Length > 20) return false;
            var digits = new StringBuilder();
            foreach (var ch in postalCode.Trim())
            {
                if (ch is >= '0' and <= '9') digits.Append(ch);
                else if (ch is >= '۰' and <= '۹') digits.Append((char)('0' + ch - '۰'));
                else if (ch is >= '٠' and <= '٩') digits.Append((char)('0' + ch - '٠'));
                else return false;
            }
            postal = digits.ToString();
            if (countryCode.Equals("IR", StringComparison.OrdinalIgnoreCase) &&
                (postal.Length != 10 || postal.All(x => x == postal[0]))) return false;
            if (postal.Length == 0) return false;
        }
        result = new(l!, countryCode.ToUpperInvariant(), p!, c!, s!, postal);
        return true;
    }

    private static bool TryText(string? raw, int min, int max, out string? value)
    {
        value = null;
        if (raw is null || raw.Length > max + 32 || raw.Any(char.IsControl)) return false;
        var normalized = raw.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length < min || normalized.Length > max) return false;
        value = normalized;
        return true;
    }
}

public sealed record NormalizedPartyAddress(
    string Label, string CountryCode, string Province, string City,
    string StreetAddress, string? PostalCode);
