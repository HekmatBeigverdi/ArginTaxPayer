using System.Data;
using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum OfficialIdentifierDecision
{
    Created, Unauthenticated, Forbidden, Invalid, ReleaseNotFound, Conflict
}

public sealed record OfficialIdentifierCandidateResult(
    OfficialIdentifierDecision Decision, Guid? IdentifierId = null,
    string? NormalizedCode = null);

/// <summary>
/// Internal, non-routed Level1-only candidate entry command.
/// No authority claim, no publication or changes to existing official entries.
/// Routes and complete administrative audit are owned by Phase06 Steps08/12.
/// </summary>
public sealed class OfficialIdentifierCandidateService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<OfficialIdentifierCandidateResult> CreateAsync(
        string? bearer, Guid stagedReleaseId, string? code, string? title,
        OfficialEntryKind kind, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(OfficialIdentifierDecision.Unauthenticated);
        if (stagedReleaseId == Guid.Empty ||
            kind is not (OfficialEntryKind.Goods or OfficialEntryKind.Service) ||
            !OfficialGoodsServiceIdentifierPolicy.TryNormalize(code, out var normalized) ||
            !OfficialGoodsServiceIdentifierPolicy.TryValidateTitle(title, out var normalizedTitle))
            return new(OfficialIdentifierDecision.Invalid);

        if (!await IsEligibleLevel1Async(actor.Value, ct))
            return new(OfficialIdentifierDecision.Forbidden);

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                // Check permission *inside* the transaction as well; role and MFA may be revoked.
                if (await identity.CurrentUserIdAsync(bearer, ct) != actor ||
                    !await IsEligibleLevel1Async(actor.Value, ct))
                    return new OfficialIdentifierCandidateResult(OfficialIdentifierDecision.Forbidden);

                var isStaged = await db.CatalogReferenceReleases.AsNoTracking()
                    .AnyAsync(r => r.Id == stagedReleaseId && r.State == 1, ct);
                if (!isStaged) return new OfficialIdentifierCandidateResult(OfficialIdentifierDecision.ReleaseNotFound);

                if (await db.OfficialGoodsServiceEntries.AnyAsync(
                    e => e.ReleaseId == stagedReleaseId && e.Kind == (int)kind && e.Code == normalized, ct))
                    return new OfficialIdentifierCandidateResult(OfficialIdentifierDecision.Conflict);

                var entry = new OfficialGoodsServiceEntry
                {
                    Id = Guid.NewGuid(), ReleaseId = stagedReleaseId,
                    Code = normalized, Title = normalizedTitle,
                    Kind = (int)kind,
                    // Owner-created candidates can NEVER become Verified here.
                    VerificationStatus = (int)OfficialVerificationStatus.Unverified
                };
                db.OfficialGoodsServiceEntries.Add(entry);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new OfficialIdentifierCandidateResult(OfficialIdentifierDecision.Created, entry.Id, normalized);
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(OfficialIdentifierDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(OfficialIdentifierDecision.Conflict);
        }
    }

    private Task<bool> IsEligibleLevel1Async(Guid actorId, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actorId && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == actorId && m.EnabledAtUtc != null), ct);
}
