using System.Data;
using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

/// <summary>
/// Server-owned, read-only invoice handoff boundary. No issuance, invoice tables,
/// tax rate inference or write operations. Persist the returned immutable snapshot
/// in the future invoice module; never dereference the mutable catalog later.
/// </summary>
public sealed class InvoiceCatalogHandoffService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private Task<bool> HasScopeAsync(Guid actor, Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(user => user.Id == actor && user.IsActive &&
            db.IdentityUsers.Any(target => target.Id == customer && target.IsActive &&
                target.Roles.Any(role => role.Role == IdentityRoleNames.Customer)) &&
            (actor == customer && user.Roles.Any(role => role.Role == IdentityRoleNames.Customer) ||
             db.CustomerCommercialOwners.Any(owner => owner.CustomerUserId == customer &&
                db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null) &&
                (owner.RootLevel1UserId == actor &&
                 user.Roles.Any(role => role.Role == IdentityRoleNames.Level1) ||
                 owner.PartnerLevel2UserId == actor &&
                 user.Roles.Any(role => role.Role == IdentityRoleNames.Level2)))), ct);

    public async Task<InvoiceCatalogHandoffResult> CaptureAsync(
        string? accessToken, Guid customerId, Guid itemId, DateTime effectiveAtUtc,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(accessToken, ct);
        if (actor is null)
            return new(InvoiceCatalogHandoffDecision.Unauthenticated);
        if (customerId == Guid.Empty || itemId == Guid.Empty ||
            effectiveAtUtc.Kind != DateTimeKind.Utc)
            return new(InvoiceCatalogHandoffDecision.Invalid);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return new(InvoiceCatalogHandoffDecision.Forbidden);

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(
                    IsolationLevel.RepeatableRead, ct);

                if (await identity.CurrentUserIdAsync(accessToken, ct) != actor ||
                    !await HasScopeAsync(actor.Value, customerId, ct))
                    return new InvoiceCatalogHandoffResult(InvoiceCatalogHandoffDecision.Forbidden);

                var item = await db.CustomerCatalogItems.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == itemId &&
                        x.CustomerUserId == customerId, ct);
                if (item is null)
                    return new InvoiceCatalogHandoffResult(InvoiceCatalogHandoffDecision.ItemNotFound);
                if (item.Status != (int)CatalogItemStatus.Active)
                    return new InvoiceCatalogHandoffResult(InvoiceCatalogHandoffDecision.ItemInactive);

                var mappings = await db.CatalogOfficialMappings.AsNoTracking()
                    .Where(x => x.ItemId == itemId && x.EffectiveFromUtc <= effectiveAtUtc &&
                        (x.EffectiveToUtc == null || x.EffectiveToUtc > effectiveAtUtc))
                    .Take(2).ToArrayAsync(ct);
                if (mappings.Length != 1)
                    return new InvoiceCatalogHandoffResult(
                        InvoiceCatalogHandoffDecision.MissingOrAmbiguousMapping);
                var mapping = mappings[0];

                var official = await db.OfficialGoodsServiceEntries.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == mapping.OfficialEntryId, ct);
                if (official is null || official.VerificationStatus != 2)
                    return new InvoiceCatalogHandoffResult(
                        InvoiceCatalogHandoffDecision.UnverifiedIdentifier);
                if (!OfficialGoodsServiceReferencePolicy.IsCompatible(
                    item.Kind, (OfficialEntryKind)official.Kind))
                    return new InvoiceCatalogHandoffResult(
                        InvoiceCatalogHandoffDecision.IncompatibleKind);

                var officialRelease = await db.CatalogReferenceReleases.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == official.ReleaseId, ct);
                var unit = await db.OfficialMeasurementUnits.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == item.BaseUnitId, ct);
                var unitRelease = unit is null ? null :
                    await db.CatalogReferenceReleases.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.Id == unit.ReleaseId, ct);

                if (officialRelease is null || officialRelease.State != 3)
                    return new InvoiceCatalogHandoffResult(
                        InvoiceCatalogHandoffDecision.UnverifiedIdentifier);
                if (unit is null || unitRelease is null ||
                    unit.Status != 1 || unitRelease.State != 3)
                    return new InvoiceCatalogHandoffResult(
                        InvoiceCatalogHandoffDecision.UnitNotApproved);

                var reviewedIds = new[] { officialRelease.Id, unitRelease.Id };
                var reviewed = await db.Database.SqlQueryRaw<Guid>(
                    """
                    SELECT release_id AS "Value" FROM catalog_release_audit
                    WHERE release_id = ANY ({0}) AND action = 'validate'
                    """, reviewedIds).ToArrayAsync(ct);
                var isOfficialReviewed = reviewed.Contains(officialRelease.Id);
                var isUnitReviewed = reviewed.Contains(unitRelease.Id);
                if (!InvoiceCatalogHandoffPolicy.CanCapture(
                    item.Kind, item.Status, official.Kind, official.VerificationStatus,
                    officialRelease.State, unit.Status, unitRelease.State,
                    isOfficialReviewed, isUnitReviewed, effectiveAtUtc,
                    mapping.EffectiveFromUtc, mapping.EffectiveToUtc))
                    return new InvoiceCatalogHandoffResult(
                        InvoiceCatalogHandoffDecision.UnverifiedIdentifier);

                var snapshot = new InvoiceCatalogHandoff(
                    InvoiceCatalogHandoffPolicy.CurrentContractVersion,
                    customerId, item.Id, item.Revision, item.InternalCode,
                    item.Title, (CatalogItemKind)item.Kind,
                    unit.Id, unit.Code, unit.Title, unitRelease.Id,
                    unitRelease.SourceDocument, unitRelease.Edition,
                    unitRelease.SourceSha256,
                    mapping.Id, mapping.Revision, mapping.EffectiveFromUtc,
                    mapping.EffectiveToUtc, official.Id, official.Code,
                    official.Title, officialRelease.Id,
                    officialRelease.SourceDocument, officialRelease.Edition,
                    officialRelease.SourceSha256, effectiveAtUtc,
                    DateTime.UtcNow, InvoiceVatResolution.Unresolved);
                if (!InvoiceCatalogHandoffPolicy.IsComplete(snapshot))
                    return new InvoiceCatalogHandoffResult(InvoiceCatalogHandoffDecision.Conflict);

                await tx.CommitAsync(ct);
                return new InvoiceCatalogHandoffResult(
                    InvoiceCatalogHandoffDecision.Success, snapshot);
            });
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            db.ChangeTracker.Clear();
            return new(InvoiceCatalogHandoffDecision.Conflict);
        }
    }
}
