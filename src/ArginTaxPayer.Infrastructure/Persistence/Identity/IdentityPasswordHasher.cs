using System.Security.Cryptography;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>Versioned PBKDF2 verifier compatible with Phase 03 Step 01 bootstrap hashes.</summary>
public static class IdentityPasswordHasher
{
    private const int Iterations = 310_000;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        Validate(password);
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1024 || string.IsNullOrEmpty(stored))
            return false;

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" ||
            !int.TryParse(parts[1], out var iterations) || iterations != Iterations)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != 32 || expected.Length != KeySize)
                return false;

            var candidate = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
            return CryptographicOperations.FixedTimeEquals(candidate, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16 || password.Length > 1024)
            throw new ArgumentException("Password must be 16 to 1024 characters.", nameof(password));
    }
}
