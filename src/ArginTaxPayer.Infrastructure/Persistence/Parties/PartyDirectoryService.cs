using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

public sealed record PartyDirectoryItem(Guid Id, PartyKind Kind, string DisplayName,
    PartyStatus Status, long Revision);
public sealed record PartyDirectoryPage(PartyDetailsDecision Decision,
    PartyDirectoryItem[] Items, int Page, int PageSize, int Total);

/// <summary>Never materialize global records before applying live customer ownership scope.</summary>
public sealed class PartyDirectoryService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<PartyDirectoryPage> ListAsync(string? bearer, Guid customerId,
        int page, int pageSize, string? query, PartyKind? kind,
        CancellationToken ct = default)
    {
        var empty = Array.Empty<PartyDirectoryItem>();
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(PartyDetailsDecision.Unauthenticated, empty, page, pageSize, 0);
        if (customerId == Guid.Empty || page < 1 || pageSize is < 1 or > 100 ||
            query?.Length > 100 || query?.Any(char.IsControl) == true ||
            kind is not null && !Enum.IsDefined(kind.Value))
            return new(PartyDetailsDecision.Invalid, empty, page, pageSize, 0);
        var authorized = await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor.Value &&
            u.IsActive && (u.Id == customerId ||
                db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null)) &&
            (u.Id == customerId && u.Roles.Any(role => role.Role == IdentityRoleNames.Customer) ||
            db.CustomerCommercialOwners.Any(o => o.CustomerUserId == customerId &&
                (u.Roles.Any(role => role.Role == IdentityRoleNames.Level1) &&
                    o.RootLevel1UserId == actor.Value ||
                 u.Roles.Any(role => role.Role == IdentityRoleNames.Level2) &&
                    o.PartnerLevel2UserId == actor.Value))), ct);
        if (!authorized) return new(PartyDetailsDecision.Forbidden, empty, page, pageSize, 0);
        var safeQuery = query?.Trim();
        var scoped = db.Parties.AsNoTracking().Where(p =>
            db.CustomerPartyLinks.Any(l => l.PartyId == p.Id &&
                l.CustomerUserId == customerId && l.UnlinkedAtUtc == null));
        if (kind is not null) scoped = scoped.Where(p => p.Kind == kind.Value);
        if (!string.IsNullOrWhiteSpace(safeQuery))
            scoped = scoped.Where(p => p.DisplayName.Contains(safeQuery) ||
                p.NormalizedIdentifier.Contains(safeQuery));
        var count = await scoped.CountAsync(ct);
        if ((long)(page - 1) * pageSize >= count)
            return new(PartyDetailsDecision.Success, empty, page, pageSize, count);
        var rows = await scoped.OrderBy(p => p.DisplayName).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PartyDirectoryItem(p.Id, p.Kind, p.DisplayName, p.Status, p.Revision))
            .ToArrayAsync(ct);
        return new(PartyDetailsDecision.Success, rows, page, pageSize, count);
    }
}
