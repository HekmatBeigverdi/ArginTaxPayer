using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Domain.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public enum TaxpayerScopeDecision { Allowed, Unauthenticated, Forbidden, NotFound, Conflict, Invalid }
public sealed record ScopedTaxpayerResult(TaxpayerScopeDecision Decision, TaxpayerProfileView? Profile = null);
public sealed record ScopedRenameRequest(string? DisplayName, long ExpectedRevision);
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
    /// Delegate can edit only a non-identity display name, not legal identifier,
    /// taxpayer status, fiscal-memory bindings or any subscription/authority data.
    /// Live grant scope is part of the atomic SQL UPDATE predicate.
    /// </summary>
    public async Task<ScopedTaxpayerResult> RenameAsync(
        string? token, Guid taxpayerId, ScopedRenameRequest? request, CancellationToken ct = default)
    {
        var actor = await ResolveAsync(token, ct);
        if (actor.Decision != TaxpayerScopeDecision.Allowed) return new(actor.Decision);
        if (request is null || request.ExpectedRevision < 1 ||
            string.IsNullOrWhiteSpace(request.DisplayName))
            return new(TaxpayerScopeDecision.Invalid);
        var name = request.DisplayName.Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (name.Length > 256 || name.Any(char.IsControl))
            return new(TaxpayerScopeDecision.Invalid);

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var previous = await ScopedQuery(actor.ActorId, actor.Level1).Where(p => p.Id == taxpayerId)
                .Select(p => new { p.Kind, p.NormalizedIdentifier, p.DisplayName, p.Status, p.Revision })
                .SingleOrDefaultAsync(ct);
            if (previous is null) return new ScopedTaxpayerResult(TaxpayerScopeDecision.NotFound);
            if (previous.Revision != request.ExpectedRevision)
                return new ScopedTaxpayerResult(TaxpayerScopeDecision.Conflict);
            if (previous.DisplayName == name || previous.Status == TaxpayerStatus.Archived)
                return new ScopedTaxpayerResult(TaxpayerScopeDecision.Invalid);
            var now = DateTime.UtcNow;
            // Recheck current authorization inside same transaction and enforce current grant
            // on UPDATE. No check-then-act unscoped taxpayer mutation is permitted.
            var current = await ResolveAsync(token, ct);
            if (current.Decision != TaxpayerScopeDecision.Allowed ||
                current.ActorId != actor.ActorId)
                return new ScopedTaxpayerResult(TaxpayerScopeDecision.Forbidden);
            var updated = await ScopedCommandQuery(actor.ActorId, current.Level1)
                .Where(p => p.Id == taxpayerId && p.Revision == request.ExpectedRevision &&
                    p.Status == previous.Status && p.DisplayName == previous.DisplayName)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.DisplayName, name)
                    .SetProperty(p => p.Revision, p => p.Revision + 1)
                    .SetProperty(p => p.UpdatedAtUtc, now), ct);
            if (updated != 1)
                return new ScopedTaxpayerResult(TaxpayerScopeDecision.Conflict);
            db.TaxpayerProfileChanges.Add(new TaxpayerProfileChange
            {
                Id = Guid.NewGuid(), TaxpayerId = taxpayerId, ActorUserId = actor.ActorId,
                FromRevision = previous.Revision, ToRevision = previous.Revision + 1,
                PriorDisplayName = previous.DisplayName, NewDisplayName = name,
                PriorStatus = previous.Status, NewStatus = previous.Status,
                ChangedAtUtc = now
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new ScopedTaxpayerResult(TaxpayerScopeDecision.Allowed,
                new TaxpayerProfileView(taxpayerId, previous.Kind, previous.NormalizedIdentifier,
                    name, previous.Status, previous.Revision + 1));
        });
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
