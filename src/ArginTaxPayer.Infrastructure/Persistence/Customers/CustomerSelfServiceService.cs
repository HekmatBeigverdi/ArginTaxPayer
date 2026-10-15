using ArginTaxPayer.Domain.Customers;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Customers;

public enum CustomerSelfServiceDecision { Success, Unauthenticated, Forbidden, Invalid }
public sealed record CustomerSelfServiceTaxpayer(Guid Id, TaxpayerKind Kind,
    string DisplayName, TaxpayerStatus Status, long Revision);
public sealed record CustomerSelfServiceDashboard(
    Guid CustomerUserId, string? DisplayName, CustomerAccountProfileStatus? ProfileStatus,
    Guid? PreferredPartyId, CustomerSelfServiceTaxpayer[] Taxpayers,
    int ActiveMembershipCount, string PolicyVersion, bool WritesEnabled);
public sealed record CustomerSelfServiceResult(CustomerSelfServiceDecision Decision,
    CustomerSelfServiceDashboard? Dashboard = null);

/// <summary>Read-only dashboard; client-provided customer IDs are never trusted.</summary>
public sealed class CustomerSelfServiceService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService authorization)
{
    public async Task<CustomerSelfServiceResult> GetMineAsync(string? bearer, CancellationToken ct = default)
    {
        var actor = await authorization.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(CustomerSelfServiceDecision.Unauthenticated);

        var isCustomer = await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actor.Value && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Customer), ct);
        if (!CustomerSelfServicePolicy.IsAllowed(
            CustomerSelfServiceAction.ViewOwnDashboard, isCustomer))
            return new(CustomerSelfServiceDecision.Forbidden);

        var profile = await db.CustomerAccountProfiles.AsNoTracking()
            .Where(p => p.CustomerUserId == actor.Value)
            .Select(p => new { p.DisplayName, p.Status, p.PreferredPartyId })
            .SingleOrDefaultAsync(ct);

        var taxpayers = await db.TaxpayerProfiles.AsNoTracking()
            .Where(t => t.CustomerUserId == actor.Value)
            .OrderBy(t => t.DisplayName).ThenBy(t => t.Id)
            .Select(t => new CustomerSelfServiceTaxpayer(
                t.Id, t.Kind, t.DisplayName, t.Status, t.Revision))
            .ToArrayAsync(ct);

        var count = await db.CustomerTaxpayerMemberships.AsNoTracking()
            .CountAsync(m => m.CustomerUserId == actor.Value && m.UnlinkedAtUtc == null &&
                db.TaxpayerProfiles.Any(t =>
                    t.Id == m.TaxpayerId && t.CustomerUserId == actor.Value), ct);

        return new(CustomerSelfServiceDecision.Success,
            new CustomerSelfServiceDashboard(actor.Value, profile?.DisplayName,
                profile?.Status, profile?.PreferredPartyId, taxpayers, count,
                CustomerSelfServicePolicy.Version, WritesEnabled: false));
    }
}
