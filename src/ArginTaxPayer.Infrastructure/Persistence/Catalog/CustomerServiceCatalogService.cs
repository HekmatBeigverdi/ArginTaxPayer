using System.Data;
using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum CustomerServiceDecision
{
    Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict, UnitUnavailable
}
public sealed record CustomerServiceView(
    Guid Id, Guid CustomerUserId, string InternalCode, string Title,
    Guid BaseUnitId, CatalogItemStatus Status, long Revision, string? OfficialCode = null);
public sealed record CustomerServiceResult(
    CustomerServiceDecision Decision, CustomerServiceView? Service = null);

/// <summary>
/// Authenticated, customer-scoped service management. All service records use Kind=3;
/// stock/nonstock goods remain managed by CustomerProductService.
/// Internal-only until authenticated catalog routes are delivered in Step12.
/// </summary>
public sealed class CustomerServiceCatalogService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private static CustomerServiceView Map(CustomerCatalogItem item) =>
        new(item.Id, item.CustomerUserId, item.InternalCode, item.Title,
            item.BaseUnitId, (CatalogItemStatus)item.Status, item.Revision);

    private Task<bool> CanManageAsync(Guid actor, Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(user =>
            user.Id == actor && user.IsActive &&
            db.IdentityUsers.Any(target =>
                target.Id == customer && target.IsActive &&
                target.Roles.Any(role => role.Role == IdentityRoleNames.Customer)) &&
            (actor == customer &&
                user.Roles.Any(role => role.Role == IdentityRoleNames.Customer) ||
             db.CustomerCommercialOwners.Any(owner =>
                owner.CustomerUserId == customer &&
                db.IdentityMfaStates.Any(m =>
                    m.UserId == actor && m.EnabledAtUtc != null) &&
                ((owner.RootLevel1UserId == actor &&
                    user.Roles.Any(role => role.Role == IdentityRoleNames.Level1)) ||
                 (owner.PartnerLevel2UserId == actor &&
                    user.Roles.Any(role => role.Role == IdentityRoleNames.Level2))))), ct);

    private Task<bool> UnitExistsAsync(Guid unitId, CancellationToken ct) =>
        db.OfficialMeasurementUnits.AsNoTracking().AnyAsync(unit =>
            unit.Id == unitId && unit.Status == 1 &&
            db.CatalogReferenceReleases.Any(release =>
                release.Id == unit.ReleaseId &&
                (release.State == 3 ||
                 (release.State == 1 && release.SourceDocument == "RC_UMGS.ST"))), ct);

    public async Task<CustomerServiceResult> CreateAsync(
        string? token, Guid customerId, CatalogServiceInput input,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CustomerServiceDecision.Unauthenticated);
        if (customerId == Guid.Empty ||
            !CustomerServicePolicy.TryValidate(input, out var clean))
            return new(CustomerServiceDecision.Invalid);
        if (!await CanManageAsync(actor.Value, customerId, ct))
            return new(CustomerServiceDecision.Forbidden);

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(token, ct) != actor ||
                    !await CanManageAsync(actor.Value, customerId, ct))
                    return new CustomerServiceResult(CustomerServiceDecision.Forbidden);
                if (!await UnitExistsAsync(clean!.BaseUnitId, ct))
                    return new CustomerServiceResult(CustomerServiceDecision.UnitUnavailable);

                var now = DateTime.UtcNow;
                var item = new CustomerCatalogItem
                {
                    Id = Guid.NewGuid(), CustomerUserId = customerId,
                    InternalCode = clean.InternalCode, Title = clean.Title,
                    Kind = (int)CatalogItemKind.Service,
                    BaseUnitId = clean.BaseUnitId, Status = (int)CatalogItemStatus.Active,
                    Revision = 1, CreatedAtUtc = now, UpdatedAtUtc = now
                };
                db.CustomerCatalogItems.Add(item);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new CustomerServiceResult(CustomerServiceDecision.Success, Map(item));
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(CustomerServiceDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(CustomerServiceDecision.Conflict);
        }
    }

    public async Task<CustomerServiceResult> GetAsync(
        string? token, Guid customerId, Guid serviceId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CustomerServiceDecision.Unauthenticated);
        if (!await CanManageAsync(actor.Value, customerId, ct))
            return new(CustomerServiceDecision.Forbidden);
        var item = await db.CustomerCatalogItems.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == serviceId && x.CustomerUserId == customerId &&
            x.Kind == (int)CatalogItemKind.Service, ct);
        return item is null
            ? new(CustomerServiceDecision.NotFound)
            : new(CustomerServiceDecision.Success, Map(item));
    }

    public async Task<(CustomerServiceDecision Decision, CustomerServiceView[] Services)>
        ListAsync(string? token, Guid customerId, int page, int pageSize,
            CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return (CustomerServiceDecision.Unauthenticated, []);
        if (page < 1 || pageSize is < 1 or > 100 ||
            (long)(page - 1) * pageSize > int.MaxValue)
            return (CustomerServiceDecision.Invalid, []);
        if (!await CanManageAsync(actor.Value, customerId, ct))
            return (CustomerServiceDecision.Forbidden, []);
        var items = await db.CustomerCatalogItems.AsNoTracking()
            .Where(x => x.CustomerUserId == customerId &&
                x.Kind == (int)CatalogItemKind.Service)
            .OrderBy(x => x.InternalCode).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return (CustomerServiceDecision.Success, items.Select(Map).ToArray());
    }

    public async Task<CustomerServiceResult> UpdateAsync(
        string? token, Guid customerId, Guid serviceId, long expectedRevision,
        CatalogServiceInput input, bool discontinue, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CustomerServiceDecision.Unauthenticated);
        if (customerId == Guid.Empty || serviceId == Guid.Empty || expectedRevision < 1 ||
            !CustomerServicePolicy.TryValidate(input, out var clean))
            return new(CustomerServiceDecision.Invalid);
        if (!await CanManageAsync(actor.Value, customerId, ct))
            return new(CustomerServiceDecision.Forbidden);

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(token, ct) != actor ||
                    !await CanManageAsync(actor.Value, customerId, ct))
                    return new CustomerServiceResult(CustomerServiceDecision.Forbidden);
                var item = await db.CustomerCatalogItems.SingleOrDefaultAsync(x =>
                    x.Id == serviceId && x.CustomerUserId == customerId &&
                    x.Kind == (int)CatalogItemKind.Service, ct);
                if (item is null) return new CustomerServiceResult(CustomerServiceDecision.NotFound);
                if (item.Revision != expectedRevision ||
                    !CustomerServicePolicy.CanEdit((CatalogItemStatus)item.Status))
                    return new CustomerServiceResult(CustomerServiceDecision.Conflict);
                if (!discontinue && !await UnitExistsAsync(clean!.BaseUnitId, ct))
                    return new CustomerServiceResult(CustomerServiceDecision.UnitUnavailable);

                if (!discontinue)
                {
                    item.InternalCode = clean!.InternalCode;
                    item.Title = clean.Title;
                    item.BaseUnitId = clean.BaseUnitId;
                }
                item.Status = discontinue ? (int)CatalogItemStatus.Discontinued : item.Status;
                item.Revision++;
                item.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new CustomerServiceResult(CustomerServiceDecision.Success, Map(item));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(CustomerServiceDecision.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(CustomerServiceDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(CustomerServiceDecision.Conflict);
        }
    }
}
