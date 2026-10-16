using System.Data;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum ReleaseChangeDecision
{
    Success, Unauthenticated, Forbidden, Invalid, NotFound, InvalidState,
    MissingReviewEvidence, MissingVerifiedEntries, Conflict
}
public sealed record ReleaseChangeResult(ReleaseChangeDecision Decision, Guid? ReleaseId = null);

/// <summary>
/// Internal administrative workflow. This service is not an HTTP endpoint.
/// Operator evidence is an attestation and MUST NOT be presented as an
/// independently verified tax-authority assertion.
/// </summary>
public sealed class CatalogReleaseActivationService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public Task<ReleaseChangeResult> ValidateAsync(string? bearer, Guid releaseId,
        string? evidenceReference, string? reason, CancellationToken ct = default) =>
        ChangeAsync(bearer, releaseId, 1, 2, "validate", evidenceReference, reason, ct);

    public Task<ReleaseChangeResult> ActivateAsync(string? bearer, Guid releaseId,
        string? evidenceReference, string? reason, CancellationToken ct = default) =>
        ChangeAsync(bearer, releaseId, 2, 3, "activate", evidenceReference, reason, ct);

    /// <summary>
    /// Emergency logical withdrawal. Supersedes current release without deleting it.
    /// It intentionally DOES NOT reactivate a previously superseded release.
    /// </summary>
    public Task<ReleaseChangeResult> WithdrawAsync(string? bearer, Guid releaseId,
        string? evidenceReference, string? reason, CancellationToken ct = default) =>
        ChangeAsync(bearer, releaseId, 3, 4, "supersede", evidenceReference, reason, ct);

    private async Task<ReleaseChangeResult> ChangeAsync(
        string? bearer, Guid releaseId, int expected, int target, string action,
        string? evidenceReference, string? reason, CancellationToken ct)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(ReleaseChangeDecision.Unauthenticated);
        if (!await CanOperateAsync(actor.Value, ct)) return new(ReleaseChangeDecision.Forbidden);
        if (releaseId == Guid.Empty || (expected, target) is not ((1, 2) or (2, 3) or (3, 4)))
            return new(ReleaseChangeDecision.Invalid);

        var evidence = evidenceReference?.Trim() ?? string.Empty;
        var explanation = reason?.Trim() ?? string.Empty;
        if (evidence.Length is < 12 or > 1000 || explanation.Length is < 12 or > 1000)
            return new(ReleaseChangeDecision.MissingReviewEvidence);

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(bearer, ct) != actor ||
                    !await CanOperateAsync(actor.Value, ct))
                    return new ReleaseChangeResult(ReleaseChangeDecision.Forbidden);

                var release = await db.CatalogReferenceReleases
                    .SingleOrDefaultAsync(r => r.Id == releaseId, ct);
                if (release is null) return new ReleaseChangeResult(ReleaseChangeDecision.NotFound);
                if (release.State != expected)
                    return new ReleaseChangeResult(ReleaseChangeDecision.InvalidState, releaseId);

                // Require an independently staged source row set; no empty releases.
                if (expected != 3)
                {
                    var unitCount = await db.OfficialMeasurementUnits.CountAsync(x => x.ReleaseId == releaseId, ct);
                    var goodsCount = await db.OfficialGoodsServiceEntries.CountAsync(x => x.ReleaseId == releaseId, ct);
                    var vatCount = await db.VatClassificationEntries.CountAsync(x => x.ReleaseId == releaseId, ct);
                    if (unitCount + goodsCount + vatCount == 0)
                        return new ReleaseChangeResult(ReleaseChangeDecision.MissingVerifiedEntries);
                    // Candidate goods/service identifiers never become verified merely
                    // from a source URL or an operator's release approval.
                    if (goodsCount > 0 && await db.OfficialGoodsServiceEntries.AnyAsync(
                        x => x.ReleaseId == releaseId && x.VerificationStatus != 2, ct))
                        return new ReleaseChangeResult(ReleaseChangeDecision.MissingVerifiedEntries);
                }

                var now = DateTime.UtcNow;
                if (target == 3)
                {
                    // Sequential release switch is atomic: old active is superseded first.
                    var old = await db.CatalogReferenceReleases
                        .Where(r => r.SourceDocument == release.SourceDocument &&
                            r.Id != release.Id && r.State == 3)
                        .ToArrayAsync(ct);
                    foreach (var previous in old)
                    {
                        previous.State = 4;
                        await InsertAuditAsync(previous.Id, actor.Value, "supersede", 3, 4,
                            evidence, "Superseded by release " + releaseId, now, ct);
                    }
                }
                release.State = target;
                if (target == 3) release.ActivatedAtUtc = now;
                await db.SaveChangesAsync(ct);
                await InsertAuditAsync(releaseId, actor.Value, action, expected, target,
                    evidence, explanation, now, ct);
                await tx.CommitAsync(ct);
                return new ReleaseChangeResult(ReleaseChangeDecision.Success, releaseId);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(ReleaseChangeDecision.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException)
        {
            db.ChangeTracker.Clear();
            return new(ReleaseChangeDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return new(ReleaseChangeDecision.Conflict);
        }
    }

    private Task<int> InsertAuditAsync(Guid releaseId, Guid actor, string action,
        int from, int to, string evidence, string reason, DateTime when,
        CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO catalog_release_audit
(id, release_id, actor_user_id, action, from_state, to_state,
 evidence_reference, reason, occurred_at_utc)
VALUES ({Guid.NewGuid()}, {releaseId}, {actor}, {action}, {from}, {to},
        {evidence}, {reason}, {when})
""", ct);

    private Task<bool> CanOperateAsync(Guid actor, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actor && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null), ct);
}
