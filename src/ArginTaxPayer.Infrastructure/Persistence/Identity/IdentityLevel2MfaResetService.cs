using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

public enum Level2MfaResetResult { Reset, Unauthenticated, Forbidden, NotFound, Conflict }

/// <summary>Level1 recovery of Level2 MFA through the existing pending-administrator enrollment flow.</summary>
public sealed class IdentityLevel2MfaResetService(
    ArginTaxPayerDbContext db, IdentitySessionService sessions, ILogger<IdentityLevel2MfaResetService> logger)
{
    public async Task<Level2MfaResetResult> ResetAsync(string? bearer, Guid targetUserId, CancellationToken ct = default)
    {
        var actorId = await sessions.AuthenticateAsync(bearer, ct);
        if (actorId is null) return Level2MfaResetResult.Unauthenticated;
        try
        {
            var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                // Check live session, role and MFA again within the reset transaction.
                if (await sessions.AuthenticateAsync(bearer, ct) != actorId ||
                    !await db.IdentityUserRoles.AnyAsync(r => r.UserId == actorId && r.Role == IdentityRoleNames.Level1, ct) ||
                    !await db.IdentityMfaStates.AnyAsync(m => m.UserId == actorId && m.EnabledAtUtc != null, ct))
                    return Level2MfaResetResult.Forbidden;
                if (actorId == targetUserId) return Level2MfaResetResult.Forbidden;
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT id FROM identity_users WHERE id = {targetUserId} FOR UPDATE", ct);
                var target = await db.IdentityUsers.AsNoTracking().Where(u => u.Id == targetUserId)
                    .Select(u => new { u.IsActive, u.MobileVerifiedAtUtc, HasPassword = u.PasswordHash != null,
                        Roles = u.Roles.Select(r => r.Role).ToArray() }).SingleOrDefaultAsync(ct);
                if (target is null) return Level2MfaResetResult.NotFound;
                if (!target.IsActive || target.MobileVerifiedAtUtc is null || !target.HasPassword ||
                    !target.Roles.Any(r => r is IdentityRoleNames.Level2 or IdentityRoleNames.PendingLevel2) ||
                    target.Roles.Any(r => r is not (IdentityRoleNames.Customer or IdentityRoleNames.Level2 or IdentityRoleNames.PendingLevel2)))
                    return Level2MfaResetResult.Forbidden;
                if (target.Roles.Contains(IdentityRoleNames.Level2) && target.Roles.Contains(IdentityRoleNames.PendingLevel2))
                    return Level2MfaResetResult.Conflict;
                if (target.Roles.Contains(IdentityRoleNames.PendingLevel2) &&
                    !await db.IdentityMfaStates.AnyAsync(m => m.UserId == targetUserId, ct))
                    return Level2MfaResetResult.Conflict;

                // No tracked mutations: execution-strategy retries cannot replay stale entities.
                await db.IdentityUserRoles.Where(r => r.UserId == targetUserId && r.Role == IdentityRoleNames.Level2)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Role, IdentityRoleNames.PendingLevel2), ct);
                await db.IdentityMfaChallenges.Where(x => x.UserId == targetUserId).ExecuteDeleteAsync(ct);
                await db.IdentityRecoveryCodes.Where(x => x.UserId == targetUserId).ExecuteDeleteAsync(ct);
                await db.IdentityMfaStates.Where(x => x.UserId == targetUserId).ExecuteDeleteAsync(ct);
                var now = DateTime.UtcNow;
                await db.IdentitySessions.Where(x => x.UserId == targetUserId && x.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);
                await tx.CommitAsync(ct);
                return Level2MfaResetResult.Reset;
            });
            if (result == Level2MfaResetResult.Reset)
                logger.LogInformation("IDENTITY_LEVEL2_MFA_RESET actor={ActorUserId} target={TargetUserId}", actorId, targetUserId);
            return result;
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            return Level2MfaResetResult.Conflict;
        }
    }
}
