using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum CatalogQueryDecision { Success, Unauthenticated, Forbidden, Invalid }
public sealed record CatalogPage<T>(CatalogQueryDecision Decision, T[] Items, int Page, int PageSize, bool HasMore);
public sealed record OfficialCatalogHit(Guid Id, Guid ReleaseId, string Code, string Title, int Kind);
public sealed record CatalogUnitHit(Guid Id, Guid ReleaseId, string Code, string Title, bool IsActive);

/// <summary>Authenticated read-only catalog search; never treats a staged code as official.</summary>
public sealed class CatalogSearchService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public static bool ValidPage(int page, int pageSize, string? query) =>
        page is >= 1 and <= 100000 && pageSize is >= 1 and <= 100 &&
        (long)(page - 1) * pageSize <= int.MaxValue &&
        (query is null || query.Trim().Length <= 120);

    private Task<bool> CanSeeCustomerAsync(Guid actor, Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(user =>
            user.Id == actor && user.IsActive &&
            db.IdentityUsers.Any(target =>
                target.Id == customer && target.IsActive &&
                target.Roles.Any(role => role.Role == IdentityRoleNames.Customer)) &&
            ((actor == customer && user.Roles.Any(role => role.Role == IdentityRoleNames.Customer)) ||
             db.CustomerCommercialOwners.Any(owner =>
                 owner.CustomerUserId == customer &&
                 db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null) &&
                 (owner.RootLevel1UserId == actor &&
                    user.Roles.Any(role => role.Role == IdentityRoleNames.Level1) ||
                  owner.PartnerLevel2UserId == actor &&
                    user.Roles.Any(role => role.Role == IdentityRoleNames.Level2)))), ct);

    public async Task<CatalogPage<CustomerProductView>> ProductsAsync(
        string? token, Guid customer, int page, int size, string? query, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CatalogQueryDecision.Unauthenticated, [], page, size, false);
        if (!ValidPage(page, size, query)) return new(CatalogQueryDecision.Invalid, [], page, size, false);
        if (!await CanSeeCustomerAsync(actor.Value, customer, ct))
            return new(CatalogQueryDecision.Forbidden, [], page, size, false);
        var filtered = db.CustomerCatalogItems.AsNoTracking()
            .Where(x => x.CustomerUserId == customer && (x.Kind == 1 || x.Kind == 2));
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            filtered = filtered.Where(x => EF.Functions.ILike(x.InternalCode, "%" + q + "%") ||
                EF.Functions.ILike(x.Title, "%" + q + "%"));
        }
        var rows = await filtered.OrderBy(x => x.InternalCode).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size + 1).ToArrayAsync(ct);
        return new(CatalogQueryDecision.Success,
            rows.Take(size).Select(x => new CustomerProductView(x.Id, x.CustomerUserId,
                x.InternalCode, x.Title, (ArginTaxPayer.Domain.Catalog.CatalogItemKind)x.Kind,
                x.BaseUnitId, (ArginTaxPayer.Domain.Catalog.CatalogItemStatus)x.Status, x.Revision)).ToArray(),
            page, size, rows.Length > size);
    }

    public async Task<CatalogPage<CustomerServiceView>> ServicesAsync(
        string? token, Guid customer, int page, int size, string? query, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CatalogQueryDecision.Unauthenticated, [], page, size, false);
        if (!ValidPage(page, size, query)) return new(CatalogQueryDecision.Invalid, [], page, size, false);
        if (!await CanSeeCustomerAsync(actor.Value, customer, ct))
            return new(CatalogQueryDecision.Forbidden, [], page, size, false);
        var filtered = db.CustomerCatalogItems.AsNoTracking()
            .Where(x => x.CustomerUserId == customer && x.Kind == 3);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            filtered = filtered.Where(x => EF.Functions.ILike(x.InternalCode, "%" + q + "%") ||
                EF.Functions.ILike(x.Title, "%" + q + "%"));
        }
        var rows = await filtered.OrderBy(x => x.InternalCode).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size + 1).ToArrayAsync(ct);
        return new(CatalogQueryDecision.Success,
            rows.Take(size).Select(x => new CustomerServiceView(x.Id, x.CustomerUserId,
                x.InternalCode, x.Title, x.BaseUnitId,
                (ArginTaxPayer.Domain.Catalog.CatalogItemStatus)x.Status, x.Revision)).ToArray(),
            page, size, rows.Length > size);
    }

    public async Task<CatalogPage<OfficialCatalogHit>> OfficialAsync(
        string? token, int page, int size, string? query, int? kind, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CatalogQueryDecision.Unauthenticated, [], page, size, false);
        if (!ValidPage(page, size, query) || (kind is not null && kind is not (1 or 2)))
            return new(CatalogQueryDecision.Invalid, [], page, size, false);
        if (!await db.IdentityUsers.AnyAsync(u => u.Id == actor && u.IsActive, ct))
            return new(CatalogQueryDecision.Forbidden, [], page, size, false);

        // Only reviewed, active, verified official goods/service identifiers.
        var hits = db.OfficialGoodsServiceEntries.AsNoTracking()
            .Where(x => x.VerificationStatus == 2 &&
                db.CatalogReferenceReleases.Any(r => r.Id == x.ReleaseId && r.State == 3) &&
                db.IdentityUserRoles.Any(role => role.UserId == actor &&
                    (role.Role == IdentityRoleNames.Level1 ||
                     role.Role == IdentityRoleNames.Level2 ||
                     role.Role == IdentityRoleNames.Customer)));
        if (kind is not null) hits = hits.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            hits = hits.Where(x => EF.Functions.ILike(x.Code, "%" + q + "%") ||
                EF.Functions.ILike(x.Title, "%" + q + "%"));
        }
        var rows = await hits.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size + 1).ToArrayAsync(ct);
        return new(CatalogQueryDecision.Success,
            rows.Take(size).Select(x => new OfficialCatalogHit(x.Id, x.ReleaseId,
                x.Code, x.Title, x.Kind)).ToArray(), page, size, rows.Length > size);
    }
    public async Task<CatalogPage<CatalogUnitHit>> UnitsAsync(
        string? token, int page, int size, string? query, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CatalogQueryDecision.Unauthenticated, [], page, size, false);
        if (!ValidPage(page, size, query))
            return new(CatalogQueryDecision.Invalid, [], page, size, false);
        if (!await db.IdentityUsers.AsNoTracking().AnyAsync(user =>
            user.Id == actor && user.IsActive && user.Roles.Any(role =>
                role.Role == IdentityRoleNames.Level1 || role.Role == IdentityRoleNames.Level2 ||
                role.Role == IdentityRoleNames.Customer), ct))
            return new(CatalogQueryDecision.Forbidden, [], page, size, false);
        var units = db.OfficialMeasurementUnits.AsNoTracking().Where(unit =>
            unit.Status == 1 && db.CatalogReferenceReleases.Any(release =>
                release.Id == unit.ReleaseId &&
                (release.State == 3 || (release.State == 1 &&
                 release.SourceDocument == "RC_UMGS.ST"))));
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            units = units.Where(x => EF.Functions.ILike(x.Title, "%" + q + "%") ||
                                    EF.Functions.ILike(x.Code, "%" + q + "%"));
        }
        var rows = await units.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size + 1)
            .Select(x => new CatalogUnitHit(x.Id, x.ReleaseId, x.Code, x.Title,
                db.CatalogReferenceReleases.Any(r => r.Id == x.ReleaseId && r.State == 3)))
            .ToArrayAsync(ct);
        return new(CatalogQueryDecision.Success, rows.Take(size).ToArray(),
            page, size, rows.Length > size);
    }

}
