using System.Data;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum ReferenceImportDecision
{
    Imported, AlreadyImported, Unauthenticated, Forbidden, InvalidPreview, Conflict
}
public sealed record ReferenceImportOutcome(ReferenceImportDecision Decision, Guid? ReleaseId = null);

/// <summary>
/// Authenticated Level1/MFA-only internal staging command.
/// Does NOT validate source authenticity, publish releases or expose routes.
/// </summary>
public sealed class ReferenceImportService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<ReferenceImportOutcome> StageAsync(string? bearer,
        ReferenceImportKind kind, string? document, string? edition,
        string? sourceReference, byte[]? uploadBytes, CancellationToken ct = default)
    {
        // Rebuild the preview from immutable upload bytes, never trust caller-provided rows/digest.
        var preview = ReferenceImportParser.Preview(kind, document, edition, sourceReference, uploadBytes);
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(ReferenceImportDecision.Unauthenticated);
        if (!await CanStageAsync(actor.Value, ct)) return new(ReferenceImportDecision.Forbidden);
        if (!preview.IsValid || preview.Sha256.Length != 64 ||
            preview.Rows.Count > ReferenceImportParser.MaxRows ||
            preview.Kind is not (ReferenceImportKind.Units or ReferenceImportKind.GoodsServices) ||
            preview.Rows.Count == 0)
            return new(ReferenceImportDecision.InvalidPreview);

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(bearer, ct) != actor ||
                    !await CanStageAsync(actor.Value, ct))
                    return new ReferenceImportOutcome(ReferenceImportDecision.Forbidden);

                var existing = await db.CatalogReferenceReleases.AsNoTracking()
                    .Where(x => x.SourceDocument == preview.SourceDocument &&
                                x.SourceSha256 == preview.Sha256)
                    .Select(x => new { x.Id, x.Edition, x.SourceReference })
                    .SingleOrDefaultAsync(ct);
                if (existing is not null)
                {
                    if (existing.Edition != preview.Edition ||
                        existing.SourceReference != preview.SourceReference)
                        return new ReferenceImportOutcome(ReferenceImportDecision.Conflict);
                    return new ReferenceImportOutcome(ReferenceImportDecision.AlreadyImported, existing.Id);
                }

                var releaseId = Guid.NewGuid();
                db.CatalogReferenceReleases.Add(new CatalogReferenceRelease
                {
                    Id = releaseId,
                    SourceDocument = preview.SourceDocument,
                    SourceSha256 = preview.Sha256,
                    SourceReference = preview.SourceReference,
                    Edition = preview.Edition,
                    State = 1,
                    ImportedAtUtc = DateTime.UtcNow
                });
                if (preview.Kind == ReferenceImportKind.Units)
                    db.OfficialMeasurementUnits.AddRange(preview.Rows.Select(x =>
                        new OfficialMeasurementUnit {
                            Id = Guid.NewGuid(), ReleaseId = releaseId,
                            Code = x.Code, Title = x.Title, Status = 1
                        }));
                else
                    db.OfficialGoodsServiceEntries.AddRange(preview.Rows.Select(x =>
                        new OfficialGoodsServiceEntry {
                            Id = Guid.NewGuid(), ReleaseId = releaseId,
                            Code = x.Code, Title = x.Title, Kind = x.Kind,
                            VerificationStatus = 1
                        }));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new ReferenceImportOutcome(ReferenceImportDecision.Imported, releaseId);
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(ReferenceImportDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(ReferenceImportDecision.Conflict);
        }
    }

    private Task<bool> CanStageAsync(Guid actor, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor && u.IsActive &&
            u.Roles.Any(x => x.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(x => x.UserId == actor && x.EnabledAtUtc != null), ct);
}
