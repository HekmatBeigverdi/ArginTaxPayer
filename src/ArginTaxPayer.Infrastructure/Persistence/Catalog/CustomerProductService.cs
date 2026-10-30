using System.Data;
using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public enum CustomerProductDecision { Success, Unauthenticated, Forbidden, Invalid, NotFound, Conflict, UnitUnavailable }
public sealed record CustomerProductView(Guid Id, Guid CustomerUserId, string InternalCode,
    string Title, CatalogItemKind Kind, Guid BaseUnitId, CatalogItemStatus Status, long Revision,
    string? OfficialCode = null);
public sealed record CustomerProductResult(CustomerProductDecision Decision, CustomerProductView? Product = null);

/// <summary>Customer-owned internal product aggregate; never maps codes to authority records implicitly.</summary>
public sealed class CustomerProductService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private static CustomerProductView Map(CustomerCatalogItem item) =>
        new(item.Id, item.CustomerUserId, item.InternalCode, item.Title,
            (CatalogItemKind)item.Kind, item.BaseUnitId, (CatalogItemStatus)item.Status, item.Revision);

    private Task<bool> HasScopeAsync(Guid actor, Guid customer, CancellationToken ct) =>
        db.IdentityUsers.AsNoTracking().AnyAsync(user => user.Id == actor && user.IsActive &&
            db.IdentityUsers.Any(target => target.Id == customer && target.IsActive &&
                target.Roles.Any(role => role.Role == IdentityRoleNames.Customer)) &&
            (actor == customer && user.Roles.Any(role => role.Role == IdentityRoleNames.Customer) ||
             db.CustomerCommercialOwners.Any(owner => owner.CustomerUserId == customer &&
                 (owner.RootLevel1UserId == actor &&
                  user.Roles.Any(role => role.Role == IdentityRoleNames.Level1) &&
                  db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null) ||
                  owner.PartnerLevel2UserId == actor &&
                  user.Roles.Any(role => role.Role == IdentityRoleNames.Level2) &&
                  db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null)))), ct);

    private Task<bool> IsUnitSelectableAsync(Guid unitId, CancellationToken ct) =>
        db.OfficialMeasurementUnits.AsNoTracking().AnyAsync(u =>
            u.Id == unitId && u.Status == 1 &&
            db.CatalogReferenceReleases.Any(r => r.Id == u.ReleaseId &&
                (r.State == 3 || r.State == 1 && r.SourceDocument == "RC_UMGS.ST")), ct);

    public async Task<CustomerProductResult> CreateAsync(string? token, Guid customerId,
        CatalogProductInput input, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CustomerProductDecision.Unauthenticated);
        if (customerId == Guid.Empty || !CustomerProductPolicy.TryValidate(input, out var clean))
            return new(CustomerProductDecision.Invalid);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return new(CustomerProductDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(token, ct) != actor ||
                    !await HasScopeAsync(actor.Value, customerId, ct))
                    return new CustomerProductResult(CustomerProductDecision.Forbidden);
                if (!await IsUnitSelectableAsync(clean!.BaseUnitId, ct))
                    return new CustomerProductResult(CustomerProductDecision.UnitUnavailable);
                var now = DateTime.UtcNow;
                var item = new CustomerCatalogItem {
                    Id = Guid.NewGuid(), CustomerUserId = customerId,
                    InternalCode = clean.InternalCode, Title = clean.Title,
                    Kind = (int)clean.Kind, BaseUnitId = clean.BaseUnitId,
                    Status = (int)CatalogItemStatus.Active, Revision = 1,
                    CreatedAtUtc = now, UpdatedAtUtc = now
                };
                db.CustomerCatalogItems.Add(item);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new CustomerProductResult(CustomerProductDecision.Success, Map(item));
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(CustomerProductDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(CustomerProductDecision.Conflict);
        }
    }

    public async Task<CustomerProductResult> GetAsync(string? token, Guid customerId,
        Guid productId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CustomerProductDecision.Unauthenticated);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return new(CustomerProductDecision.Forbidden);
        var product = await db.CustomerCatalogItems.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == productId && p.CustomerUserId == customerId &&
                (p.Kind == 1 || p.Kind == 2), ct);
        return product is null
            ? new(CustomerProductDecision.NotFound)
            : new(CustomerProductDecision.Success, Map(product));
    }

    public async Task<(CustomerProductDecision Decision, CustomerProductView[] Products)>
        ListAsync(string? token, Guid customerId, int page, int pageSize,
            CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return (CustomerProductDecision.Unauthenticated, []);
        if (page < 1 || pageSize is < 1 or > 100 ||
            (long)(page - 1) * pageSize > int.MaxValue)
            return (CustomerProductDecision.Invalid, []);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return (CustomerProductDecision.Forbidden, []);
        var items = await db.CustomerCatalogItems.AsNoTracking()
            .Where(p => p.CustomerUserId == customerId && (p.Kind == 1 || p.Kind == 2))
            .OrderBy(p => p.InternalCode).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToArrayAsync(ct);
        return (CustomerProductDecision.Success, items.Select(Map).ToArray());
    }

    public async Task<CustomerProductResult> UpdateAsync(string? token, Guid customerId,
        Guid productId, long expectedRevision, CatalogProductInput input, bool discontinue,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(token, ct);
        if (actor is null) return new(CustomerProductDecision.Unauthenticated);
        if (customerId == Guid.Empty || productId == Guid.Empty || expectedRevision < 1 ||
            !CustomerProductPolicy.TryValidate(input, out var clean))
            return new(CustomerProductDecision.Invalid);
        if (!await HasScopeAsync(actor.Value, customerId, ct))
            return new(CustomerProductDecision.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(token, ct) != actor ||
                    !await HasScopeAsync(actor.Value, customerId, ct))
                    return new CustomerProductResult(CustomerProductDecision.Forbidden);
                var item = await db.CustomerCatalogItems.SingleOrDefaultAsync(p =>
                    p.Id == productId && p.CustomerUserId == customerId &&
                    (p.Kind == 1 || p.Kind == 2), ct);
                if (item is null) return new CustomerProductResult(CustomerProductDecision.NotFound);
                if (item.Revision != expectedRevision || !CustomerProductPolicy.CanEdit((CatalogItemStatus)item.Status))
                    return new CustomerProductResult(CustomerProductDecision.Conflict);
                if (!CustomerProductPolicy.CanChangeKind((CatalogItemKind)item.Kind, clean!.Kind))
                    return new CustomerProductResult(CustomerProductDecision.Invalid);
                if (!discontinue && !await IsUnitSelectableAsync(clean.BaseUnitId, ct))
                    return new CustomerProductResult(CustomerProductDecision.UnitUnavailable);
                if (!discontinue)
                {
                    item.InternalCode = clean.InternalCode;
                    item.Title = clean.Title;
                    item.BaseUnitId = clean.BaseUnitId;
                }
                item.Status = discontinue ? (int)CatalogItemStatus.Discontinued : item.Status;
                item.Revision++;
                item.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new CustomerProductResult(CustomerProductDecision.Success, Map(item));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(CustomerProductDecision.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            db.ChangeTracker.Clear();
            return new(CustomerProductDecision.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            db.ChangeTracker.Clear();
            return new(CustomerProductDecision.Conflict);
        }
    }
}
