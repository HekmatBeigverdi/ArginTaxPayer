using System.Globalization;
using System.Text;

namespace ArginTaxPayer.Domain.Catalog;

/// <summary>
/// Invoice sstid structural format from owner-provided RC_IITP.IS V7.9.
/// Structural validity does NOT mean the authority recognizes the code.
/// </summary>
public static class OfficialGoodsServiceIdentifierPolicy
{
    public const int Length = 13;

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        if (trimmed.Length != Length) return false;

        var result = new StringBuilder(Length);
        foreach (var ch in trimmed)
        {
            if (ch >= '0' && ch <= '9') result.Append(ch);
            else if (ch >= '\u06f0' && ch <= '\u06f9') result.Append((char)('0' + ch - '\u06f0'));
            else if (ch >= '\u0660' && ch <= '\u0669') result.Append((char)('0' + ch - '\u0660'));
            else return false;
        }
        normalized = result.ToString();
        return true;
    }

    public static bool TryValidateTitle(string? title, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(title)) return false;
        var clean = title.Trim().Normalize(NormalizationForm.FormC);
        if (clean.Length is < 2 or > 256 || clean.Any(char.IsControl)) return false;
        normalized = clean;
        return true;
    }
}

/// <summary>Tax authority goods/service class, distinct from ArginAccounting Stock/NonStock/Service.</summary>
public enum OfficialEntryKind { Goods = 1, Service = 2 }
public enum OfficialVerificationStatus { Unverified = 1, Verified = 2 }

public static class OfficialGoodsServiceReferencePolicy
{
    public static bool CanBeTreatedAsVerified(
        OfficialVerificationStatus entryVerification,
        int releaseState, bool trustedSourceEvidence)
        => entryVerification == OfficialVerificationStatus.Verified &&
           releaseState == 3 && trustedSourceEvidence;

    public static bool IsCompatible(int customerItemKind, OfficialEntryKind officialKind) =>
        customerItemKind switch
        {
            1 or 2 => officialKind == OfficialEntryKind.Goods,
            3 => officialKind == OfficialEntryKind.Service,
            _ => false
        };
}
