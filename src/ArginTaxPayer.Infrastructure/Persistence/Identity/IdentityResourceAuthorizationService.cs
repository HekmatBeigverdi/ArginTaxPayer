using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>
/// Resource classes resolved from authoritative persistence, not from user-supplied
/// owner/tenant IDs. New taxpayer classes must supply a trusted database resolver
/// in Phase 04; an unknown class is denied by default.
/// </summary>
public enum IdentityResourceKind
{
    IdentityUser,
    IdentitySession
}

public enum ResourceAccessDecision
{
    Unauthenticated,
    Forbidden,
    NotFound,
    Allowed
}

/// <summary>
/// Trusted ownership lookup. Implementations must filter by current actor ID in the
/// storage query, not inspect an untrusted owner ID passed by the HTTP client.
/// </summary>
public interface IIdentityResourceOwnershipResolver
{
    Task<bool> ExistsAsync(IdentityResourceKind kind, Guid resourceId, CancellationToken ct = default);
    Task<bool> IsOwnedByAsync(IdentityResourceKind kind, Guid resourceId, Guid actorId,
        CancellationToken ct = default);
}

/// <summary>Phase 03 persisted identity resource resolver. No arbitrary taxpayer scope grants.</summary>
public sealed class IdentityResourceOwnershipResolver(ArginTaxPayerDbContext db)
    : IIdentityResourceOwnershipResolver
{
    public Task<bool> ExistsAsync(IdentityResourceKind kind, Guid resourceId,
        CancellationToken ct = default) => kind switch
    {
        IdentityResourceKind.IdentityUser => db.IdentityUsers.AsNoTracking()
            .AnyAsync(x => x.Id == resourceId && x.IsActive, ct),
        IdentityResourceKind.IdentitySession => db.IdentitySessions.AsNoTracking()
            .AnyAsync(x => x.Id == resourceId && x.User.IsActive, ct),
        _ => Task.FromResult(false)
    };

    public Task<bool> IsOwnedByAsync(IdentityResourceKind kind, Guid resourceId,
        Guid actorId, CancellationToken ct = default) => kind switch
    {
        IdentityResourceKind.IdentityUser => db.IdentityUsers.AsNoTracking()
            .AnyAsync(x => x.Id == resourceId && x.Id == actorId && x.IsActive, ct),
        IdentityResourceKind.IdentitySession => db.IdentitySessions.AsNoTracking()
            .AnyAsync(x => x.Id == resourceId && x.UserId == actorId && x.User.IsActive, ct),
        _ => Task.FromResult(false)
    };
}

/// <summary>
/// Combines currently valid session, live database role and storage-derived ownership.
/// Cross-owner and missing records have indistinguishable 404 responses.
/// </summary>
public sealed class IdentityResourceAuthorizationService(
    ArginTaxPayerDbContext db,
    IdentityAuthorizationService authorization,
    IIdentityResourceOwnershipResolver ownership)
{
    public async Task<ResourceAccessDecision> CheckAsync(string? bearer,
        string permission, IdentityResourceKind kind, Guid resourceId,
        CancellationToken ct = default)
    {
        var principal = await authorization.CurrentUserIdAsync(bearer, ct);
        if (principal is null) return ResourceAccessDecision.Unauthenticated;
        if (await authorization.CheckAsync(bearer, permission, ct) != IdentityAuthorizationResult.Allowed)
            return ResourceAccessDecision.Forbidden;
        if (resourceId == Guid.Empty ||
            !await ownership.IsOwnedByAsync(kind, resourceId, principal.Value, ct))
            return ResourceAccessDecision.NotFound;
        return ResourceAccessDecision.Allowed;
    }

    public async Task<IReadOnlyList<Guid>?> ListOwnSessionIdsAsync(string? bearer,
        CancellationToken ct = default)
    {
        var principal = await authorization.CurrentUserIdAsync(bearer, ct);
        if (principal is null) return null;
        if (await authorization.CheckAsync(bearer, IdentityPermissions.ReadOwnIdentity, ct)
            != IdentityAuthorizationResult.Allowed)
            return Array.Empty<Guid>();

        var now = DateTime.UtcNow;
        return await db.IdentitySessions.AsNoTracking()
            .Where(x => x.UserId == principal.Value && x.RevokedAtUtc == null &&
                x.RefreshExpiresAtUtc > now)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<ResourceAccessDecision> RevokeOwnSessionAsync(string? bearer,
        Guid sessionId, CancellationToken ct = default)
    {
        var decision = await CheckAsync(bearer, IdentityPermissions.ReadOwnIdentity,
            IdentityResourceKind.IdentitySession, sessionId, ct);
        if (decision != ResourceAccessDecision.Allowed) return decision;
        var actorId = await authorization.CurrentUserIdAsync(bearer, ct);
        if (actorId is null) return ResourceAccessDecision.Unauthenticated;

        var updated = await db.IdentitySessions
            .Where(x => x.Id == sessionId && x.UserId == actorId.Value &&
                x.RevokedAtUtc == null && x.User.IsActive)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow), ct);
        // Idempotent and non-revealing: a previously revoked own resource returns 404.
        return updated == 1 ? ResourceAccessDecision.Allowed : ResourceAccessDecision.NotFound;
    }
}
