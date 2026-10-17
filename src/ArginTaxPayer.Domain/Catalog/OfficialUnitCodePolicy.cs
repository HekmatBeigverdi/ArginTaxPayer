namespace ArginTaxPayer.Domain.Catalog;

/// <summary>
/// Invoice mu is a numeric string of at most eight digits under RC_IITP.IS V7.9.
/// Membership in a source release is checked separately; syntax alone is insufficient.
/// </summary>
public static class OfficialUnitCodePolicy
{
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var value = input.Trim();
        if (value.Length is < 1 or > 8) return false;
        var digits = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            digits[i] = ch switch
            {
                >= '0' and <= '9' => ch,
                >= '\u06F0' and <= '\u06F9' => (char)('0' + ch - '\u06F0'),
                >= '\u0660' and <= '\u0669' => (char)('0' + ch - '\u0660'),
                _ => '\0'
            };
            if (digits[i] == '\0') return false;
        }
        normalized = new string(digits);
        return true;
    }

    /// <summary>Conversions are item-specific ArginAccounting rational numbers, not global unit ratios.</summary>
    public static bool ValidConversion(long numerator, long denominator, DateTime fromUtc, DateTime? toUtc) =>
        numerator > 0 && denominator > 0 &&
        fromUtc.Kind == DateTimeKind.Utc &&
        (toUtc is null || (toUtc.Value.Kind == DateTimeKind.Utc && toUtc.Value > fromUtc));
}
