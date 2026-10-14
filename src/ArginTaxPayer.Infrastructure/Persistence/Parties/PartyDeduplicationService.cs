using System.Data;
using System.Text;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public enum PartyReviewStatus { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record PartyMatch(Guid Id, PartyKind Kind, string DisplayName, long Revision, string MatchReason);
public sealed record PartyMatchResult(PartyReviewStatus Status, PartyMatch[] Matches);
public sealed record PartyReviewResult(PartyReviewStatus Status, Guid? ReviewId = null);

/// <summary>
/// Fail-closed, customer-scoped matching and human classification.
/// Neither operation alters identity, commercial ownership, links, or fiscal history.
/// </summary>
public sealed class PartyDeduplicationService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private async Task<bool> IsScopedAdminAsync(Guid actor, Guid customer, CancellationToken ct)
    {
        if (customer == Guid.Empty) return false;
        return await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor && u.IsActive &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null) &&
            db.CustomerCommercialOwners.Any(o => o.CustomerUserId == customer &&
                (u.Roles.Any(role => role.Role == IdentityRoleNames.Level1) &&
                    o.RootLevel1UserId == actor ||
                 u.Roles.Any(role => role.Role == IdentityRoleNames.Level2) &&
                    o.PartnerLevel2UserId == actor)), ct);
    }

    public async Task<PartyMatchResult> FindCandidatesAsync(string? bearer,
        Guid customerId, Guid candidatePartyId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(PartyReviewStatus.Unauthenticated, []);
        if (!await IsScopedAdminAsync(actor.Value, customerId, ct))
            return new(PartyReviewStatus.Forbidden, []);
        var target = await db.Parties.AsNoTracking().Where(p => p.Id == candidatePartyId &&
            db.CustomerPartyLinks.Any(l => l.PartyId == p.Id && l.CustomerUserId == customerId &&
                l.UnlinkedAtUtc == null)).SingleOrDefaultAsync(ct);
        if (target is null) return new(PartyReviewStatus.NotFound, []);
        var name = target.DisplayName.Trim().Normalize(NormalizationForm.FormC);
        // Never discover or disclose parties owned by other customer accounts.
        var matches = await db.Parties.AsNoTracking().Where(p =>
                p.Id != candidatePartyId && p.Kind == target.Kind &&
                (p.NormalizedIdentifier == target.NormalizedIdentifier || p.DisplayName == name) &&
                db.CustomerPartyLinks.Any(l => l.PartyId == p.Id &&
                    l.CustomerUserId == customerId && l.UnlinkedAtUtc == null))
            .OrderBy(p => p.Id).Take(50)
            .Select(p => new PartyMatch(p.Id, p.Kind, p.DisplayName, p.Revision,
                p.NormalizedIdentifier == target.NormalizedIdentifier ? "identifier_exact" : "name_exact"))
            .ToArrayAsync(ct);
        return new(PartyReviewStatus.Success, matches);
    }

    public async Task<PartyReviewResult> ReviewAsync(string? bearer, Guid customerId,
        Guid candidateId, Guid existingId, PartyDuplicateDecision decision,
        string? reason, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(PartyReviewStatus.Unauthenticated);
        if (candidateId == Guid.Empty || existingId == Guid.Empty || candidateId == existingId ||
            customerId == Guid.Empty || !Enum.IsDefined(decision) ||
            reason is null || reason.Any(char.IsControl) || reason.Trim().Length is < 3 or > 500)
            return new(PartyReviewStatus.Invalid);
        if (!await IsScopedAdminAsync(actor.Value, customerId, ct))
            return new(PartyReviewStatus.Forbidden);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await IsScopedAdminAsync(actor.Value, customerId, ct))
            return new(PartyReviewStatus.Forbidden);
        var rows = await db.Parties.AsNoTracking().Where(p =>
            (p.Id == candidateId || p.Id == existingId) &&
            db.CustomerPartyLinks.Any(l => l.PartyId == p.Id &&
                l.CustomerUserId == customerId && l.UnlinkedAtUtc == null))
            .Select(p => new { p.Id, p.Kind }).ToArrayAsync(ct);
        if (rows.Length != 2) return new(PartyReviewStatus.NotFound);
        if (rows[0].Kind != rows[1].Kind) return new(PartyReviewStatus.Conflict);
        var entry = new PartyDuplicateReview
        {
            Id = Guid.NewGuid(), CustomerUserId = customerId,
            CandidatePartyId = candidateId, ExistingPartyId = existingId,
            ReviewerUserId = actor.Value, Decision = decision,
            Reason = reason.Trim().Normalize(NormalizationForm.FormC),
            ReviewedAtUtc = DateTime.UtcNow
        };
        db.PartyDuplicateReviews.Add(entry);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(PartyReviewStatus.Success, entry.Id);
    }
}
