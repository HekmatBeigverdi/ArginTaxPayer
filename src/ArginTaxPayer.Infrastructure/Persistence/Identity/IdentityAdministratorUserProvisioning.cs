using Microsoft.EntityFrameworkCore;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.Extensions.Logging;
using Npgsql;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using System.Security.Cryptography;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

public enum AdminUserCreationStatus { Created, Invalid, Unauthenticated, Forbidden, Conflict }

/// <summary>
/// Privileged customer provisioning for existing Level 1/2 operators.
/// Never creates or elevates administrators, and never bypasses the admin's MFA.
/// </summary>
public sealed class IdentityAdministratorUserProvisioning(
    ArginTaxPayerDbContext db, IdentitySessionService sessions,
    ILogger<IdentityAdministratorUserProvisioning> logger,
    CustomerCommercialOwnershipService ownership, SmsCreditService? smsCredit = null)
{
    public async Task<AdminUserCreationStatus> CreateCustomerAsync(
        string? bearer, string? mobileInput, string? emailInput,
        string? password, string? firstName = null, string? lastName = null,
        string? companyName = null, string? address = null, CancellationToken ct = default)
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
        var hasProfile = firstName is not null || lastName is not null || companyName is not null || address is not null;
        if (hasProfile)
        {
            if (!ValidProfilePart(firstName, 100, true) || !ValidProfilePart(lastName, 100, true) ||
                !ValidProfilePart(address, 500, true) || !ValidProfilePart(companyName, 256, false))
                return AdminUserCreationStatus.Invalid;
            password ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }
        try { IdentityPasswordHasher.Validate(password ?? ""); }
        catch (ArgumentException) { return AdminUserCreationStatus.Invalid; }

        if (await db.IdentityUsers.AsNoTracking().AnyAsync(
            x => x.NormalizedMobile == mobile || email != null && x.NormalizedEmail == email, ct))
            return AdminUserCreationStatus.Conflict;

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var initialOwner = await ownership.PrepareInitialAsync(userId, operatorId, ct);
        if (initialOwner is null) return AdminUserCreationStatus.Forbidden;
        db.CustomerCommercialOwners.Add(initialOwner);
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
        if (hasProfile)
        {
            db.SoftwareUserProfiles.Add(new SoftwareUserProfile {
                UserId = userId, FirstName = firstName!.Trim().Normalize(), LastName = lastName!.Trim().Normalize(),
                CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim().Normalize(),
                Address = address!.Trim().Normalize(), CompletedAtUtc = now, UpdatedAtUtc = now
            });
        }
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            if(smsCredit is not null) await smsCredit.GrantInitialCustomerCreditWithinTransactionAsync(userId, now, ct);
            await transaction.CommitAsync(ct);
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

    private static bool ValidProfilePart(string? value, int max, bool required)
    {
        if (value is null) return !required;
        if (value.Length > max * 2 || value.Any(char.IsControl)) return false;
        var trimmed = value.Trim();
        return trimmed.Length <= max && (!required || trimmed.Length >= 2);
    }
}
