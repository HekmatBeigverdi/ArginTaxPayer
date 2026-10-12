using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Explicit one-time provisioning only: do not invoke on application startup or expose as an HTTP endpoint.
/// The operator supplies the initial password outside code/logs after applying the migration manually.
/// </summary>
public static class InitialAdministratorBootstrap
{
    public static async Task<Guid> ProvisionAsync(
        ArginTaxPayerDbContext db, string email, string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16 ||
            password.Length > 1024)
            throw new ArgumentException("A password of 16 to 1024 characters is required.", nameof(password));

        // Fail closed: never automatically provision a second Level 1 identity.
        if (await db.IdentityUserRoles.AnyAsync(x => x.Role == IdentityRoleNames.Level1, cancellationToken))
            throw new InvalidOperationException("An initial Level 1 administrator already exists.");

        if (await db.IdentityUsers.AnyAsync(x => x.NormalizedEmail == normalized, cancellationToken))
            throw new InvalidOperationException("An account with that email already exists.");

        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        db.IdentityUsers.Add(new IdentityUser
        {
            Id = id,
            NormalizedEmail = normalized,
            PasswordHash = HashPassword(password),
            IsActive = true,
            CreatedAtUtc = now,
            PasswordChangedAtUtc = now,
            Roles = new List<IdentityUserRole>
            {
                new() { UserId = id, Role = IdentityRoleNames.Level1, AssignedAtUtc = now }
            }
        });
        // Unique email is database-enforced. Concurrent first-admin provision operations
        // must be serialized by an operator / database advisory lock in the provisioning CLI.
        await db.SaveChangesAsync(cancellationToken);
        return id;
    }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        var normalized = email.Trim().ToUpperInvariant();
        if (normalized.Length > 320 || normalized.Contains(' ') ||
            normalized.Count(c => c == '@') != 1 ||
            normalized.StartsWith('@') || normalized.EndsWith('@'))
            throw new ArgumentException("Invalid email address.", nameof(email));
        return normalized;
    }

    public static string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(32);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 310_000, HashAlgorithmName.SHA256, 32);
        return "pbkdf2-sha256$310000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(key);
    }
}
