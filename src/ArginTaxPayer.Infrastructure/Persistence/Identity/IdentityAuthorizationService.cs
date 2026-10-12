using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Server-owned role-to-permission matrix. Never derive authorization from request bodies
/// or client-supplied role claims. Permission checks query current PostgreSQL roles.
/// </summary>
public static class IdentityPermissions
{
    public const string ReadOwnIdentity = "identity.self.read";
    public const string ReadRoleDirectory = "identity.roles.read";
    public const string ManageLevel2 = "identity.roles.level2.manage";
    public const string ReadAdministration = "administration.read";
    public const string ReadCustomerArea = "customer.read";

    public static readonly IReadOnlyDictionary<string, string[]> RolePermissions =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [IdentityRoleNames.Level1] = [
                ReadOwnIdentity, ReadRoleDirectory, ManageLevel2,
                ReadAdministration, ReadCustomerArea
            ],
            [IdentityRoleNames.Level2] = [
                ReadOwnIdentity, ReadRoleDirectory, ReadAdministration, ReadCustomerArea
            ],
            [IdentityRoleNames.Customer] = [ReadOwnIdentity, ReadCustomerArea]
        };

    public static bool Grants(string role, string permission) =>
        RolePermissions.TryGetValue(role, out var grants) &&
        grants.Contains(permission, StringComparer.Ordinal);
}

public enum IdentityAuthorizationResult
{
    Unauthenticated,
    Forbidden,
    Allowed
}

public enum Level2RoleGrantResult
{
    Unauthenticated,
    Forbidden,
    TargetNotFound,
    Conflict,
    Granted
}

/// <summary>
/// Database-backed authorization and narrow role assignment.
/// All privileged decisions re-evaluate live role and MFA state; a successful role
/// grant invalidates the target's existing sessions to prevent stale privilege changes.
/// </summary>
public sealed class IdentityAuthorizationService(
    ArginTaxPayerDbContext db, IdentitySessionService sessions)
{
    public async Task<Guid?> CurrentUserIdAsync(string? accessToken, CancellationToken ct = default)
        => await sessions.AuthenticateAsync(accessToken, ct);

    public async Task<IdentityAuthorizationResult> CheckAsync(
        string? accessToken, string permission, CancellationToken ct = default)
    {
        var userId = await sessions.AuthenticateAsync(accessToken, ct);
        if (userId is null) return IdentityAuthorizationResult.Unauthenticated;
        if (!IdentityPermissions.RolePermissions.Values.Any(
            grants => grants.Contains(permission, StringComparer.Ordinal)))
            return IdentityAuthorizationResult.Forbidden;

        var roles = await db.IdentityUserRoles.AsNoTracking()
            .Where(r => r.UserId == userId.Value)
            .Select(r => r.Role)
            .ToArrayAsync(ct);
        return roles.Any(r => IdentityPermissions.Grants(r, permission))
            ? IdentityAuthorizationResult.Allowed
            : IdentityAuthorizationResult.Forbidden;
    }

    public async Task<string[]?> GetCurrentRolesAsync(
        string? accessToken, CancellationToken ct = default)
    {
        var userId = await sessions.AuthenticateAsync(accessToken, ct);
        if (userId is null) return null;
        return await db.IdentityUserRoles.AsNoTracking()
            .Where(r => r.UserId == userId.Value)
            .Select(r => r.Role)
            .OrderBy(r => r)
            .ToArrayAsync(ct);
    }

    public async Task<Level2RoleGrantResult> GrantLevel2Async(
        string? actorAccessToken, Guid targetUserId, CancellationToken ct = default)
    {
        var actorId = await sessions.AuthenticateAsync(actorAccessToken, ct);
        if (actorId is null) return Level2RoleGrantResult.Unauthenticated;
        if (actorId.Value == targetUserId) return Level2RoleGrantResult.Forbidden;

        // Only a current, active Level 1 with confirmed MFA can grant Level 2.
        var actorCanGrant = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actorId.Value && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct);
        if (!actorCanGrant) return Level2RoleGrantResult.Forbidden;

        // Do not implicitly change the role of an existing Level 1 or a disabled account.
        var target = await db.IdentityUsers.AsNoTracking()
            .Where(u => u.Id == targetUserId)
            .Select(u => new { u.IsActive, Roles = u.Roles.Select(r => r.Role).ToArray() })
            .SingleOrDefaultAsync(ct);
        if (target is null || !target.IsActive) return Level2RoleGrantResult.TargetNotFound;
        if (target.Roles.Contains(IdentityRoleNames.Level1))
            return Level2RoleGrantResult.Forbidden;
        if (target.Roles.Contains(IdentityRoleNames.Level2))
            return Level2RoleGrantResult.Conflict;
        if (!target.Roles.Contains(IdentityRoleNames.Customer))
            return Level2RoleGrantResult.Forbidden;

        // A newly promoted administrator must have MFA set up BEFORE elevation.
        if (!await db.IdentityMfaStates.AsNoTracking().AnyAsync(m =>
            m.UserId == targetUserId && m.EnabledAtUtc != null, ct))
            return Level2RoleGrantResult.Forbidden;

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
            // Recheck privileged actor and target under transaction isolation.
            var actorStillAllowed = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
                u.Id == actorId.Value && u.IsActive &&
                u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
                db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct);
            var targetStillAllowed = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
                u.Id == targetUserId && u.IsActive &&
                u.Roles.Any(r => r.Role == IdentityRoleNames.Customer) &&
                !u.Roles.Any(r => r.Role == IdentityRoleNames.Level1 ||
                    r.Role == IdentityRoleNames.Level2) &&
                db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct);
            if (!actorStillAllowed || !targetStillAllowed)
                return Level2RoleGrantResult.Forbidden;

            db.IdentityUserRoles.Add(new IdentityUserRole
            {
                UserId = targetUserId,
                Role = IdentityRoleNames.Level2,
                AssignedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            await db.IdentitySessions.Where(s => s.UserId == targetUserId && s.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
            await tx.CommitAsync(ct);
            return Level2RoleGrantResult.Granted;
        });
    }
}
