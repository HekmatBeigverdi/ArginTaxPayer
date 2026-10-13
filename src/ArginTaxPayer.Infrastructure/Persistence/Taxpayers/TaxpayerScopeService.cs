using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Domain.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public enum TaxpayerScopeDecision { Allowed, Unauthenticated, Forbidden, NotFound }
public sealed record ScopedTaxpayerResult(TaxpayerScopeDecision Decision, TaxpayerProfileView? Profile = null);
public sealed record ScopedTaxpayerListResult(TaxpayerScopeDecision Decision, TaxpayerProfileView[]? Profiles = null);

/// <summary>
/// Re-evaluates the live session, active role and non-revoked grants in every DB query.
/// Caller-controlled taxpayer IDs never become implicit access grants.
/// </summary>
public sealed class TaxpayerScopeService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<(TaxpayerScopeDecision Decision, Guid ActorId, bool Level1)> ResolveAsync(
        string? token, CancellationToken ct = default)
    {
        var id = await identity.CurrentUserIdAsync(token, ct);
        if (id is null) return (TaxpayerScopeDecision.Unauthenticated, Guid.Empty, false);
        var roles = await db.IdentityUsers.AsNoTracking().Where(u =>
            u.Id == id.Value && u.IsActive &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null))
            .Select(u => new
            {
                Root = u.Roles.Any(r => r.Role == IdentityRoleNames.Level1),
                Delegate = u.Roles.Any(r => r.Role == IdentityRoleNames.Level2)
            }).SingleOrDefaultAsync(ct);
        if (roles is null || (!roles.Root && !roles.Delegate))
            return (TaxpayerScopeDecision.Forbidden, Guid.Empty, false);
        return (TaxpayerScopeDecision.Allowed, id.Value, roles.Root);
    }

    public IQueryable<TaxpayerProfile> ScopedQuery(Guid actorId, bool level1)
    {
        var profiles = db.TaxpayerProfiles.AsNoTracking();
        if (level1) return profiles;
        return profiles.Where(p => db.TaxpayerDelegateGrants.Any(g =>
            g.TaxpayerId == p.Id && g.DelegateUserId == actorId &&
            g.RevokedAtUtc == null));
    }

    public async Task<ScopedTaxpayerListResult> ListAsync(string? token, CancellationToken ct = default)
    {
        var principal = await ResolveAsync(token, ct);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return new(principal.Decision);
        var profiles = await ScopedQuery(principal.ActorId, principal.Level1)
            .Where(p => p.Status != TaxpayerStatus.Archived)
            .OrderBy(p => p.DisplayName).ThenBy(p => p.Id)
            .Select(p => new TaxpayerProfileView(p.Id, p.Kind, p.NormalizedIdentifier,
                p.DisplayName, p.Status, p.Revision)).ToArrayAsync(ct);
        return new(TaxpayerScopeDecision.Allowed, profiles);
    }

    public async Task<ScopedTaxpayerResult> GetAsync(
        string? token, Guid taxpayerId, CancellationToken ct = default)
    {
        var principal = await ResolveAsync(token, ct);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return new(principal.Decision);
        var profile = await ScopedQuery(principal.ActorId, principal.Level1)
            .Where(p => p.Id == taxpayerId)
            .Select(p => new TaxpayerProfileView(p.Id, p.Kind, p.NormalizedIdentifier,
                p.DisplayName, p.Status, p.Revision)).SingleOrDefaultAsync(ct);
        return profile is null ? new(TaxpayerScopeDecision.NotFound) :
            new(TaxpayerScopeDecision.Allowed, profile);
    }

    /// <summary>
    /// Server-enforced predicate for future mutations. Use inside the same SQL
    /// UPDATE/transaction; do NOT authorize once then perform an unscoped write.
    /// </summary>
    public IQueryable<TaxpayerProfile> ScopedCommandQuery(Guid actorId, bool level1) =>
        level1 ? db.TaxpayerProfiles :
            db.TaxpayerProfiles.Where(p => db.TaxpayerDelegateGrants.Any(g =>
                g.TaxpayerId == p.Id && g.DelegateUserId == actorId && g.RevokedAtUtc == null));
}
