using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public sealed record UpdateTaxpayerProfileRequest(string? DisplayName, TaxpayerStatus Status, long ExpectedRevision);
public sealed record TaxpayerProfileView(Guid Id, TaxpayerKind Kind, string NormalizedIdentifier, string DisplayName, TaxpayerStatus Status, long Revision);
public enum TaxpayerLifecycleOutcome { Updated, Invalid, Unauthenticated, Forbidden, NotFound, Conflict }
public sealed record TaxpayerLifecycleResult(TaxpayerLifecycleOutcome Outcome, TaxpayerProfileView? Profile = null);

/// <summary>Revision-checked, atomic profile edit and append-only history.</summary>
public sealed class TaxpayerProfileLifecycleService(ArginTaxPayerDbContext db, IdentityAuthorizationService authorization)
{
    public async Task<TaxpayerLifecycleResult> UpdateAsync(
        string? bearer, Guid taxpayerId, UpdateTaxpayerProfileRequest? request, CancellationToken ct = default)
    {
        var actor = await authorization.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(TaxpayerLifecycleOutcome.Unauthenticated);

        // Step 04 only exposes Level 1 writes. Delegate authorization awaits Step 08/09.
        var allowed = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actor.Value && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct);
        if (!allowed) return new(TaxpayerLifecycleOutcome.Forbidden);

        if (taxpayerId == Guid.Empty || request is null || request.ExpectedRevision < 1 ||
            !Enum.IsDefined(request.Status) || string.IsNullOrWhiteSpace(request.DisplayName))
            return new(TaxpayerLifecycleOutcome.Invalid);
        var name = request.DisplayName.Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (name.Length > 256 || name.Any(char.IsControl)) return new(TaxpayerLifecycleOutcome.Invalid);

        var old = await db.TaxpayerProfiles.AsNoTracking()
            .Where(p => p.Id == taxpayerId)
            .Select(p => new { p.Kind, p.NormalizedIdentifier, p.DisplayName, p.Status, p.Revision })
            .SingleOrDefaultAsync(ct);
        if (old is null) return new(TaxpayerLifecycleOutcome.NotFound);
        if (old.Revision != request.ExpectedRevision) return new(TaxpayerLifecycleOutcome.Conflict);
        if (old.Status != request.Status &&
            !TaxpayerLifecyclePolicy.CanTransition(old.Status, request.Status))
            return new(TaxpayerLifecycleOutcome.Invalid);
        if (old.Status == request.Status && old.DisplayName == name)
            return new(TaxpayerLifecycleOutcome.Invalid);

        // ExecuteUpdate with revision CAS is used instead of EF tracking to prevent stale writes.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Recheck live rights inside transaction.
            if (!await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
                u.Id == actor.Value && u.IsActive &&
                u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
                db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct))
                return new TaxpayerLifecycleResult(TaxpayerLifecycleOutcome.Forbidden);
            var now = DateTime.UtcNow;
            var changed = await db.TaxpayerProfiles.Where(p =>
                p.Id == taxpayerId && p.Revision == request.ExpectedRevision &&
                p.Status == old.Status && p.DisplayName == old.DisplayName)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.DisplayName, name)
                    .SetProperty(x => x.Status, request.Status)
                    .SetProperty(x => x.Revision, x => x.Revision + 1)
                    .SetProperty(x => x.UpdatedAtUtc, now), ct);
            if (changed != 1) return new TaxpayerLifecycleResult(TaxpayerLifecycleOutcome.Conflict);

            db.Set<TaxpayerProfileChange>().Add(new TaxpayerProfileChange
            {
                Id = Guid.NewGuid(), TaxpayerId = taxpayerId, ActorUserId = actor.Value,
                FromRevision = old.Revision, ToRevision = old.Revision + 1,
                PriorDisplayName = old.DisplayName, NewDisplayName = name,
                PriorStatus = old.Status, NewStatus = request.Status, ChangedAtUtc = now
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new TaxpayerLifecycleResult(TaxpayerLifecycleOutcome.Updated,
                new TaxpayerProfileView(taxpayerId, old.Kind, old.NormalizedIdentifier,
                    name, request.Status, old.Revision + 1));
        });
    }
}
