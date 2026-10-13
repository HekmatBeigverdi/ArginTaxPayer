namespace ArginTaxPayer.Domain.Taxpayers;

/// <summary>Local syntax checks only; no government identity verification is implied.</summary>
public static class IranianTaxpayerIdentifier
{
    public static bool TryNormalize(TaxpayerKind kind, string? input, out string normalized)
    {
        normalized = string.Empty;
        if (input is null || input.Length > 40) return false;
        var chars = new List<char>(input.Length);
        foreach (var c in input.Trim())
        {
            if (c is >= '0' and <= '9') chars.Add(c);
            else if (c is >= '۰' and <= '۹') chars.Add((char)('0' + c - '۰'));
            else if (c is >= '٠' and <= '٩') chars.Add((char)('0' + c - '٠'));
            else return false;
        }
        var value = new string(chars.ToArray());
        if (value.Length != (kind == TaxpayerKind.NaturalPerson ? 10 : 11) ||
            value.All(c => c == value[0])) return false;
        if (kind == TaxpayerKind.NaturalPerson)
        {
            var sum = 0;
            for (var i = 0; i < 9; i++) sum += (value[i] - '0') * (10 - i);
            var r = sum % 11;
            if ((value[9] - '0') != (r < 2 ? r : 11 - r)) return false;
        }
        else if (kind != TaxpayerKind.LegalEntity) return false;
        // Legal-entity checksums vary across public implementations. Do not reject a
        // potentially valid official 11-digit ID without an authoritative registry check.
        normalized = value;
        return true;
    }
}
