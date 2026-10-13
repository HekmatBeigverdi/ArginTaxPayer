using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Domain.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public sealed record GrantDelegateRequest(Guid DelegateUserId);
public sealed record RevokeDelegateRequest(long ExpectedRevision, string? Reason);
public enum DelegateOperationOutcome { Success, Invalid, Unauthenticated, Forbidden, NotFound, Conflict }
public sealed record DelegateOperationResult(DelegateOperationOutcome Outcome, TaxpayerDelegateGrant? Grant = null);

public sealed class TaxpayerDelegateService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private async Task<Guid?> Level1Async(string? token, CancellationToken ct)
    {
        var id = await identity.CurrentUserIdAsync(token, ct);
        if (id is null) return null;
        return await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == id && u.IsActive && u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct)
            ? id : null;
    }

    private async Task<DelegateOperationOutcome?> DeniedAsync(string? token, CancellationToken ct)
    {
        if (await identity.CurrentUserIdAsync(token, ct) is null)
            return DelegateOperationOutcome.Unauthenticated;
        return await Level1Async(token, ct) is null ? DelegateOperationOutcome.Forbidden : null;
    }

    public async Task<DelegateOperationResult> GrantAsync(string? token, Guid taxpayerId,
        GrantDelegateRequest request, CancellationToken ct = default)
    {
        if (await DeniedAsync(token, ct) is { } denied) return new(denied);
        if (taxpayerId == Guid.Empty || request.DelegateUserId == Guid.Empty)
            return new(DelegateOperationOutcome.Invalid);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, ct);
                var actor = await Level1Async(token, ct);
                if (actor is null) return new DelegateOperationResult(DelegateOperationOutcome.Forbidden);
                if (!await db.TaxpayerProfiles.AsNoTracking().AnyAsync(t =>
                    t.Id == taxpayerId && t.Status == TaxpayerStatus.Active, ct))
                    return new DelegateOperationResult(DelegateOperationOutcome.NotFound);
                if (actor == request.DelegateUserId ||
                    !await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
                        u.Id == request.DelegateUserId && u.IsActive &&
                        u.Roles.Any(x => x.Role == IdentityRoleNames.Level2) &&
                        !u.Roles.Any(x => x.Role == IdentityRoleNames.Level1) &&
                        db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct))
                    return new DelegateOperationResult(DelegateOperationOutcome.Forbidden);
                if (await db.TaxpayerDelegateGrants.AsNoTracking().AnyAsync(g =>
                    g.TaxpayerId == taxpayerId && g.DelegateUserId == request.DelegateUserId &&
                    g.RevokedAtUtc == null, ct))
                    return new DelegateOperationResult(DelegateOperationOutcome.Conflict);
                var row = new TaxpayerDelegateGrant {
                    Id = Guid.NewGuid(), TaxpayerId = taxpayerId,
                    DelegateUserId = request.DelegateUserId,
                    GrantedByLevel1UserId = actor.Value, GrantedAtUtc = DateTime.UtcNow
                };
                db.TaxpayerDelegateGrants.Add(row);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new DelegateOperationResult(DelegateOperationOutcome.Success, row);
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(DelegateOperationOutcome.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(DelegateOperationOutcome.Conflict);
        }
    }

    public async Task<DelegateOperationResult> RevokeAsync(string? token, Guid taxpayerId,
        Guid grantId, RevokeDelegateRequest request, CancellationToken ct = default)
    {
        if (await DeniedAsync(token, ct) is { } denied) return new(denied);
        if (taxpayerId == Guid.Empty || grantId == Guid.Empty || request.ExpectedRevision < 1 ||
            string.IsNullOrWhiteSpace(request.Reason))
            return new(DelegateOperationOutcome.Invalid);
        var reason = request.Reason.Trim();
        if (reason.Length > 500 || reason.Any(char.IsControl)) return new(DelegateOperationOutcome.Invalid);
        var prior = await db.TaxpayerDelegateGrants.AsNoTracking()
            .SingleOrDefaultAsync(g => g.TaxpayerId == taxpayerId && g.Id == grantId, ct);
        if (prior is null) return new(DelegateOperationOutcome.NotFound);
        if (prior.RevokedAtUtc is not null || prior.Revision != request.ExpectedRevision)
            return new(DelegateOperationOutcome.Conflict);
        var actor = await Level1Async(token, ct);
        if (actor is null) return new(DelegateOperationOutcome.Forbidden);
        var now = DateTime.UtcNow;
        var changed = await db.TaxpayerDelegateGrants.Where(g => g.Id == grantId &&
            g.TaxpayerId == taxpayerId && g.RevokedAtUtc == null && g.Revision == request.ExpectedRevision)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevokedByLevel1UserId, actor.Value)
                .SetProperty(x => x.RevocationReason, reason)
                .SetProperty(x => x.Revision, x => x.Revision + 1), ct);
        if (changed != 1) return new(DelegateOperationOutcome.Conflict);
        prior.RevokedAtUtc = now;
        prior.RevokedByLevel1UserId = actor;
        prior.RevocationReason = reason;
        prior.Revision++;
        return new(DelegateOperationOutcome.Success, prior);
    }

    public async Task<(DelegateOperationOutcome Outcome, TaxpayerDelegateGrant[]? History)>
        HistoryAsync(string? token, Guid taxpayerId, CancellationToken ct = default)
    {
        if (await DeniedAsync(token, ct) is { } denied) return (denied, null);
        if (!await db.TaxpayerProfiles.AsNoTracking().AnyAsync(t => t.Id == taxpayerId, ct))
            return (DelegateOperationOutcome.NotFound, null);
        var rows = await db.TaxpayerDelegateGrants.AsNoTracking()
            .Where(g => g.TaxpayerId == taxpayerId).OrderByDescending(g => g.GrantedAtUtc)
            .ToArrayAsync(ct);
        return (DelegateOperationOutcome.Success, rows);
    }
}
