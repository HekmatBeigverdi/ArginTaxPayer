using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>Development-only credential proof; session issuance is implemented in Step 03.</summary>
public sealed class IdentityCredentialService(ArginTaxPayerDbContext db)
{
    public async Task<IdentityRegistrationResult> RegisterCustomerAsync(
        string? email, string? password, CancellationToken cancellationToken = default)
    {
        string normalized;
        try
        {
            normalized = InitialAdministratorBootstrap.NormalizeEmail(email ?? "");
            IdentityPasswordHasher.Validate(password ?? "");
        }
        catch (ArgumentException)
        {
            return IdentityRegistrationResult.Invalid;
        }

        // No client-supplied role is accepted; registration always receives Customer.
        // Unique email constraint protects against concurrent registration.
        var exists = await db.IdentityUsers.AnyAsync(u => u.NormalizedEmail == normalized, cancellationToken);
        if (exists)
            return IdentityRegistrationResult.Conflict;

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.IdentityUsers.Add(new IdentityUser
        {
            Id = id, NormalizedEmail = normalized,
            PasswordHash = IdentityPasswordHasher.Hash(password!),
            IsActive = true, CreatedAtUtc = now, PasswordChangedAtUtc = now,
            Roles = new List<IdentityUserRole>
            {
                new() { UserId = id, Role = IdentityRoleNames.Customer, AssignedAtUtc = now }
            }
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return IdentityRegistrationResult.Created;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return IdentityRegistrationResult.Conflict;
        }
    }

    public async Task<bool> VerifyCredentialsAsync(
        string? email, string? password, CancellationToken cancellationToken = default)
    {
        // No user enumeration details may escape to callers.
        if (string.IsNullOrEmpty(password) || password.Length > 1024)
            return false;

        string? normalizedEmail = null;
        string? normalizedMobile = null;
        if (IranianMobileNumber.TryNormalize(email, out var mobile)) normalizedMobile = mobile;
        else { try { normalizedEmail = InitialAdministratorBootstrap.NormalizeEmail(email ?? ""); }
            catch (ArgumentException) { return false; } }

        var user = await db.IdentityUsers.AsNoTracking()
            .Where(u => normalizedEmail != null && u.NormalizedEmail == normalizedEmail ||
                normalizedMobile != null && u.NormalizedMobile == normalizedMobile && u.MobileVerifiedAtUtc != null)
            .Select(u => new { u.PasswordHash, u.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        return user is { IsActive: true } &&
            IdentityPasswordHasher.Verify(password, user.PasswordHash);
    }
}

public enum IdentityRegistrationResult
{
    Created, Invalid, Conflict
}
