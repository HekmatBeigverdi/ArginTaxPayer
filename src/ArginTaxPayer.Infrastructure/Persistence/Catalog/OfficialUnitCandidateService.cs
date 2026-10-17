using System.Data;
using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum OfficialUnitDecision { Created, Unauthenticated, Forbidden, Invalid, ReleaseNotStaged, Conflict }
public sealed record OfficialUnitResult(OfficialUnitDecision Decision, Guid? UnitId = null, string? Code = null);

/// <summary>
/// Internal candidate-only command (no HTTP routes). Only Level1 + MFA may define codes,
/// and only on a staged release. Published reference rows are never updated in-place.
/// Import/activation audit and public management UI are deferred to Steps07/08/12/13.
/// </summary>
public sealed class OfficialUnitCandidateService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<OfficialUnitResult> CreateAsync(string? bearer, Guid stagedReleaseId,
        string? code, string? title, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(OfficialUnitDecision.Unauthenticated);
        if (stagedReleaseId == Guid.Empty ||
            !OfficialUnitCodePolicy.TryNormalize(code, out var canonical) ||
            !OfficialGoodsServiceIdentifierPolicy.TryValidateTitle(title, out var cleanTitle))
            return new(OfficialUnitDecision.Invalid);
        if (!await Level1WithMfa(actor.Value, ct)) return new(OfficialUnitDecision.Forbidden);

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(bearer, ct) != actor ||
                    !await Level1WithMfa(actor.Value, ct))
                    return new OfficialUnitResult(OfficialUnitDecision.Forbidden);
                if (!await db.CatalogReferenceReleases.AnyAsync(
                    x => x.Id == stagedReleaseId && x.State == 1, ct))
                    return new OfficialUnitResult(OfficialUnitDecision.ReleaseNotStaged);
                if (await db.OfficialMeasurementUnits.AnyAsync(
                    x => x.ReleaseId == stagedReleaseId && x.Code == canonical, ct))
                    return new OfficialUnitResult(OfficialUnitDecision.Conflict);

                var unit = new OfficialMeasurementUnit
                {
                    Id = Guid.NewGuid(), ReleaseId = stagedReleaseId,
                    Code = canonical, Title = cleanTitle, Status = 1
                };
                db.OfficialMeasurementUnits.Add(unit);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new OfficialUnitResult(OfficialUnitDecision.Created, unit.Id, canonical);
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(OfficialUnitDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(OfficialUnitDecision.Conflict);
        }
    }

    private Task<bool> Level1WithMfa(Guid actor, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null), ct);
}
