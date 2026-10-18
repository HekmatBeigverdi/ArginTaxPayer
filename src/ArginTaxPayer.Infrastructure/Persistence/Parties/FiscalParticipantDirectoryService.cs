using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Parties;

/// <summary>
/// Paged fiscal counterparties. Identity accounts are NEVER projected as financial records.
/// Buyer and seller remain distinct persisted aggregates; UNION ALL only affects read projection.
/// </summary>
public sealed record FiscalParticipantRow(Guid Id, Guid CustomerUserId, string Type,
    int Kind, string DisplayName, string Identifier, int Status, DateTime CreatedAtUtc);
public sealed record FiscalParticipantPage(
    PartyDetailsDecision Decision, FiscalParticipantRow[] Items, int Page, int PageSize, int Total);

public sealed class FiscalParticipantDirectoryService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<FiscalParticipantPage> ListAsync(string? bearer, int page, int pageSize,
        string? type, int? kind, string? query, CancellationToken ct = default,
        Guid? customerId = null)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null)
            return new(PartyDetailsDecision.Unauthenticated, [], page, pageSize, 0);
        if (page < 1 || pageSize is < 1 or > 100 ||
            type is not (null or "" or "all" or "buyer" or "taxpayer") ||
            kind is not (null or 1 or 2) ||
            query?.Length > 100 || query?.Any(char.IsControl) == true)
            return new(PartyDetailsDecision.Invalid, [], page, pageSize, 0);

        // This subquery is evaluated by PostgreSQL, not after fetching global customer IDs.
        // Access is recomputed for every read against current roles, MFA and commercial ownership.
        var allowedCustomers =
            from customer in db.IdentityUsers.AsNoTracking()
            where customer.IsActive && customer.Roles.Any(r => r.Role == IdentityRoleNames.Customer)
            where db.IdentityUsers.Any(actorUser =>
                actorUser.Id == actor.Value && actorUser.IsActive &&
                (actorUser.Id == customer.Id &&
                    actorUser.Roles.Any(r => r.Role == IdentityRoleNames.Customer) ||
                 db.IdentityMfaStates.Any(m =>
                    m.UserId == actor.Value && m.EnabledAtUtc != null) &&
                 (actorUser.Roles.Any(r => r.Role == IdentityRoleNames.Level1) ||
                 db.CustomerCommercialOwners.Any(o =>
                    o.CustomerUserId == customer.Id &&
                    actorUser.Roles.Any(r => r.Role == IdentityRoleNames.Level2) &&
                        o.PartnerLevel2UserId == actor.Value))))
            select customer.Id;

        // Concat primitive SQL projections BEFORE constructing DTOs; this keeps UNION ALL
        // and pagination fully translatable by EF Core/Npgsql.
        var buyers =
            from link in db.CustomerPartyLinks.AsNoTracking()
            join party in db.Parties.AsNoTracking() on link.PartyId equals party.Id
            where link.UnlinkedAtUtc == null && allowedCustomers.Contains(link.CustomerUserId) &&
                (!customerId.HasValue || link.CustomerUserId == customerId.Value)
            select new
            {
                party.Id, link.CustomerUserId, Type = "buyer", Kind = (int)party.Kind,
                party.DisplayName, Identifier = party.NormalizedIdentifier,
                Status = (int)party.Status, party.CreatedAtUtc
            };

        var sellers = db.TaxpayerProfiles.AsNoTracking()
            .Where(p => p.CustomerUserId.HasValue &&
                allowedCustomers.Contains(p.CustomerUserId.Value) &&
                (!customerId.HasValue || p.CustomerUserId == customerId.Value))
            .Select(p => new
            {
                p.Id, CustomerUserId = p.CustomerUserId!.Value, Type = "taxpayer",
                Kind = (int)p.Kind, p.DisplayName,
                Identifier = p.NormalizedIdentifier, Status = (int)p.Status,
                p.CreatedAtUtc
            });

        var records = type switch
        {
            "buyer" => buyers,
            "taxpayer" => sellers,
            _ => buyers.Concat(sellers)
        };

        if (kind.HasValue) records = records.Where(r => r.Kind == kind.Value);
        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            records = records.Where(r =>
                r.DisplayName.Contains(term) || r.Identifier.Contains(term));

        var count = await records.CountAsync(ct);
        var items = await records.OrderByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Type).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new FiscalParticipantRow(r.Id, r.CustomerUserId, r.Type,
                r.Kind, r.DisplayName, r.Identifier, r.Status, r.CreatedAtUtc))
            .ToArrayAsync(ct);
        return new(PartyDetailsDecision.Success, items, page, pageSize, count);
    }
}
