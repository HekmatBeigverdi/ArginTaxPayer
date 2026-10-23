namespace ArginTaxPayer.Domain.Signing;

/// <summary>Pure state policy matching the persisted Phase09 operation state values.</summary>
public static class SigningOperationPolicy
{
    public const string LegacyProfile = "legacy-key-source-0.0.30-rsa-sha256-pkcs1-base64";
    public const string ConsentVersion = "server-custody-v1";

    public static bool CanAdvance(short from, short to) => (from, to) switch
    {
        (1, 2 or 4 or 5) => true,
        (2, 3 or 4 or 5) => true,
        (5, 2 or 3 or 4) => true,
        _ => false
    };

    public static bool ValidScope(Guid customer, Guid taxpayer, Guid memory) =>
        customer != Guid.Empty && taxpayer != Guid.Empty && memory != Guid.Empty;

    public static bool ValidFingerprint(string? value) =>
        value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool FreshAuthentication(DateTime loginUtc, DateTime nowUtc) =>
        loginUtc.Kind == DateTimeKind.Utc && nowUtc.Kind == DateTimeKind.Utc &&
        loginUtc <= nowUtc && nowUtc - loginUtc <= TimeSpan.FromMinutes(5);
}
