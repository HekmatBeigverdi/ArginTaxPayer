namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Converts Iranian mobile-number input into one canonical international identity.
/// This is deliberately independent of the SMS provider and stores no secrets.
/// A syntactically valid number is NOT proof of ownership.
/// </summary>
public static class IranianMobileNumber
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64) return false;

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (ch is ' ' or '-' or '\u200c' or '\u00a0') continue;
            if (ch is >= '0' and <= '9') { builder.Append(ch); continue; }
            if (ch is >= '\u06f0' and <= '\u06f9')
            {
                builder.Append((char)('0' + ch - '\u06f0'));
                continue;
            }
            if (ch is >= '\u0660' and <= '\u0669')
            {
                builder.Append((char)('0' + ch - '\u0660'));
                continue;
            }
            if (ch == '+' && builder.Length == 0) { builder.Append(ch); continue; }
            return false;
        }

        var digits = builder.ToString();
        if (digits.StartsWith("0098", StringComparison.Ordinal))
            digits = digits[4..];
        else if (digits.StartsWith("+98", StringComparison.Ordinal))
            digits = digits[3..];
        else if (digits.StartsWith("98", StringComparison.Ordinal))
            digits = digits[2..];
        else if (digits.StartsWith("0", StringComparison.Ordinal))
            digits = digits[1..];

        if (digits.Length != 10 || digits[0] != '9' || !digits.All(c => c is >= '0' and <= '9'))
            return false;

        normalized = "+98" + digits;
        return true;
    }
}
