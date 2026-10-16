using System.Data;
using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum TaxMappingDecision
{
    Success, Unauthenticated, Forbidden, Invalid, NotFound, WrongKind,
    UnverifiedOfficialCode, Overlap, Conflict
}
public sealed record TaxMappingView(
    Guid Id, Guid ItemId, Guid OfficialEntryId, string OfficialCode, string OfficialTitle,
    Guid OfficialReleaseId, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc, long Revision);
public sealed record TaxMappingResult(TaxMappingDecision Decision, TaxMappingView? Mapping = null);

/// <summary>
/// Tenant-authorized, historical mapping command. An official entry must belong to an
/// activated release and have independently established verification state.
/// This does not issue or validate invoices or rewrite past mapping identities.
/// </summary>
public sealed class TaxIdentifierMappingService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private Task<bool> HasScopeAsync(Guid actor, Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(user => user.Id == actor && user.IsActive &&
            db.IdentityUsers.Any(target => target.Id == customer && target.IsActive &&
                target.Roles.Any(role => role.Role == IdentityRoleNames.Customer)) &&
            (user.Id == customer && user.Roles.Any(role => role.Role == IdentityRoleNames.Customer) ||
             db.CustomerCommercialOwners.Any(owner => owner.CustomerUserId == customer &&
                db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null) &&
                (owner.RootLevel1UserId == actor &&
                 user.Roles.Any(role => role.Role == IdentityRoleNames.Level1) ||
                 owner.PartnerLevel2UserId == actor &&
                 user.Roles.Any(role => role.Role == IdentityRoleNames.Level2)))), ct);

    private async Task<TaxMappingView> MapAsync(CatalogOfficialMapping mapping, CancellationToken ct)
    {
        var official = await db.OfficialGoodsServiceEntries.AsNoTracking()
            .Where(x => x.Id == mapping.OfficialEntryId)
            .Select(x => new { x.Code, x.Title, x.ReleaseId })
            .SingleAsync(ct);
        return new(mapping.Id, mapping.ItemId, mapping.OfficialEntryId,
            official.Code, official.Title, official.ReleaseId,
            mapping.EffectiveFromUtc, mapping.EffectiveToUtc, mapping.Revision);
    }

    public async Task<TaxMappingResult> AssignAsync(string? token, Guid customerId,
        Guid itemId, Guid officialId, DateTime fromUtc, DateTime? toUtc = null,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(TaxMappingDecision.Unauthenticated);
        var window = new TaxIdentifierMappingWindow(fromUtc, toUtc);
        if (customerId == Guid.Empty || itemId == Guid.Empty || officialId == Guid.Empty ||
            !TaxIdentifierMappingPolicy.IsValidWindow(window))
            return new(TaxMappingDecision.Invalid);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return new(TaxMappingDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(token, ct) != actor ||
                    !await HasScopeAsync(actor.Value, customerId, ct))
                    return new TaxMappingResult(TaxMappingDecision.Forbidden);
                var item = await db.CustomerCatalogItems.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == itemId && x.CustomerUserId == customerId, ct);
                if (item is null) return new TaxMappingResult(TaxMappingDecision.NotFound);
                if (item.Status != (int)CatalogItemStatus.Active)
                    return new TaxMappingResult(TaxMappingDecision.Invalid);
                var official = await db.OfficialGoodsServiceEntries.AsNoTracking()
                    .Where(x => x.Id == officialId)
                    .Select(x => new {
                        x.Id, x.Code, x.Kind, x.VerificationStatus, x.ReleaseId,
                        ReleaseState = db.CatalogReferenceReleases
                            .Where(r => r.Id == x.ReleaseId).Select(r => r.State).FirstOrDefault()
                    }).SingleOrDefaultAsync(ct);
                if (official is null) return new TaxMappingResult(TaxMappingDecision.NotFound);
                if (!OfficialGoodsServiceReferencePolicy.IsCompatible(item.Kind,
                        (OfficialEntryKind)official.Kind))
                    return new TaxMappingResult(TaxMappingDecision.WrongKind);
                // Operator-reviewed source evidence must exist in the immutable release audit.
                // This is an internal trust gate, not a cryptographic government validation.
                var reviewed = await db.Database.SqlQueryRaw<int>(
                    """
                    SELECT 1 AS "Value" FROM catalog_release_audit
                    WHERE release_id = {0} AND action = 'validate'
                    LIMIT 1
                    """, official.ReleaseId).AnyAsync(ct);
                if (!TaxIdentifierMappingPolicy.CanMap(item.Kind, official.Kind,
                        official.VerificationStatus, official.ReleaseState,
                        hasTrustedSourceEvidence: reviewed, itemIsActive: true))
                    return new TaxMappingResult(TaxMappingDecision.UnverifiedOfficialCode);

                var conflict = await db.CatalogOfficialMappings.AsNoTracking()
                    .AnyAsync(x => x.ItemId == itemId &&
                        x.EffectiveFromUtc < (toUtc ?? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)) &&
                        (x.EffectiveToUtc == null || x.EffectiveToUtc > fromUtc), ct);
                if (conflict) return new TaxMappingResult(TaxMappingDecision.Overlap);
                var mapping = new CatalogOfficialMapping
                {
                    Id = Guid.NewGuid(), ItemId = itemId, OfficialEntryId = officialId,
                    EffectiveFromUtc = fromUtc, EffectiveToUtc = toUtc, Revision = 1
                };
                db.CatalogOfficialMappings.Add(mapping);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new TaxMappingResult(TaxMappingDecision.Success, await MapAsync(mapping, ct));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(TaxMappingDecision.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException)
        {
            db.ChangeTracker.Clear();
            return new(TaxMappingDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return new(TaxMappingDecision.Conflict);
        }
    }

    public async Task<(TaxMappingDecision Decision, TaxMappingView[] History)>
        HistoryAsync(string? token, Guid customerId, Guid itemId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return (TaxMappingDecision.Unauthenticated, []);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return (TaxMappingDecision.Forbidden, []);
        if (!await db.CustomerCatalogItems.AsNoTracking().AnyAsync(
                x => x.Id == itemId && x.CustomerUserId == customerId, ct))
            return (TaxMappingDecision.NotFound, []);
        var records = await db.CatalogOfficialMappings.AsNoTracking()
            .Where(x => x.ItemId == itemId).OrderBy(x => x.EffectiveFromUtc)
            .ThenBy(x => x.Id).ToArrayAsync(ct);
        var officialIds = records.Select(record => record.OfficialEntryId).Distinct().ToArray();
        var entries = await db.OfficialGoodsServiceEntries.AsNoTracking()
            .Where(x => officialIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        return (TaxMappingDecision.Success, records.Select(m =>
        {
            var e = entries[m.OfficialEntryId];
            return new TaxMappingView(m.Id, m.ItemId, e.Id, e.Code, e.Title, e.ReleaseId,
                m.EffectiveFromUtc, m.EffectiveToUtc, m.Revision);
        }).ToArray());
    }
    public async Task<TaxMappingResult> CloseAsync(string? token, Guid customerId,
        Guid mappingId, long expectedRevision, DateTime closedAtUtc, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(TaxMappingDecision.Unauthenticated);
        if (customerId == Guid.Empty || mappingId == Guid.Empty || expectedRevision < 1 ||
            closedAtUtc.Kind != DateTimeKind.Utc)
            return new(TaxMappingDecision.Invalid);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return new(TaxMappingDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(token, ct) != actor ||
                    !await HasScopeAsync(actor.Value, customerId, ct))
                    return new TaxMappingResult(TaxMappingDecision.Forbidden);
                var mapping = await db.CatalogOfficialMappings
                    .SingleOrDefaultAsync(x => x.Id == mappingId &&
                        db.CustomerCatalogItems.Any(item => item.Id == x.ItemId &&
                            item.CustomerUserId == customerId), ct);
                if (mapping is null) return new TaxMappingResult(TaxMappingDecision.NotFound);
                if (mapping.Revision != expectedRevision)
                    return new TaxMappingResult(TaxMappingDecision.Conflict);
                if (!TaxIdentifierMappingPolicy.CanClose(
                    new(mapping.EffectiveFromUtc, mapping.EffectiveToUtc), closedAtUtc))
                    return new TaxMappingResult(TaxMappingDecision.Invalid);
                mapping.EffectiveToUtc = closedAtUtc;
                mapping.Revision++;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new TaxMappingResult(TaxMappingDecision.Success, await MapAsync(mapping, ct));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(TaxMappingDecision.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException)
        {
            db.ChangeTracker.Clear();
            return new(TaxMappingDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(TaxMappingDecision.Conflict);
        }
    }

}
