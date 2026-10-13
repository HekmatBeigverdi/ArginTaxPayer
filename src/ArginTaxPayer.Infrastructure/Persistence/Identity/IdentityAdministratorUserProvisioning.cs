using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

public enum AdminUserCreationStatus { Created, Invalid, Unauthenticated, Forbidden, Conflict }

/// <summary>
/// Privileged customer provisioning for existing Level 1/2 operators.
/// Never creates or elevates administrators, and never bypasses the admin's MFA.
/// </summary>
public sealed class IdentityAdministratorUserProvisioning(
    ArginTaxPayerDbContext db, IdentitySessionService sessions,
    ILogger<IdentityAdministratorUserProvisioning> logger)
{
    public async Task<AdminUserCreationStatus> CreateCustomerAsync(
        string? bearer, string? mobileInput, string? emailInput,
        string? password, CancellationToken ct = default)
    {
        var operatorId = await sessions.AuthenticateAsync(bearer, ct);
        if (operatorId is null) return AdminUserCreationStatus.Unauthenticated;
        // Require a live operator with Level 1/2 role and an already-enabled TOTP MFA.
        var allowed = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == operatorId.Value && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1 || r.Role == IdentityRoleNames.Level2) &&
            db.IdentityMfaStates.Any(m => m.UserId == operatorId.Value && m.EnabledAtUtc != null), ct);
        if (!allowed) return AdminUserCreationStatus.Forbidden;

        if (!IranianMobileNumber.TryNormalize(mobileInput, out var mobile))
            return AdminUserCreationStatus.Invalid;
        string? email = null;
        if (!string.IsNullOrWhiteSpace(emailInput))
        {
            try { email = InitialAdministratorBootstrap.NormalizeEmail(emailInput); }
            catch (ArgumentException) { return AdminUserCreationStatus.Invalid; }
        }
        try { IdentityPasswordHasher.Validate(password ?? ""); }
        catch (ArgumentException) { return AdminUserCreationStatus.Invalid; }

        if (await db.IdentityUsers.AsNoTracking().AnyAsync(
            x => x.NormalizedMobile == mobile || email != null && x.NormalizedEmail == email, ct))
            return AdminUserCreationStatus.Conflict;

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.IdentityUsers.Add(new IdentityUser {
            Id = userId, NormalizedMobile = mobile, NormalizedEmail = email,
            // Trusted administrative provisioning explicitly bypasses SMS proof.
            // The operator is accountable for verifying recipient ownership out-of-band.
            MobileVerifiedAtUtc = now, PasswordHash = IdentityPasswordHasher.Hash(password!),
            PasswordChangedAtUtc = now, CreatedAtUtc = now, IsActive = true,
            Roles = new List<IdentityUserRole> {
                new() { UserId = userId, Role = IdentityRoleNames.Customer, AssignedAtUtc = now }
            }
        });
        try
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("IDENTITY_ADMIN_CUSTOMER_CREATED; trusted_manual_mobile_onboarding=true");
            return AdminUserCreationStatus.Created;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return AdminUserCreationStatus.Conflict;
        }
    }
}
