using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Root and partner own different effective price windows. Ownership is
/// resolved server side; clients cannot nominate another partner's price owner.</summary>
public sealed class SubscriptionPriceAdministrationService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity,
    SubscriptionCatalogRepository catalog)
{
    public async Task<StoredSubscriptionPrice> CreateAsync(string? bearer,
        Guid planId, long priceIrr, DateTime fromUtc, DateTime? untilUtc,
        CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles=await identity.GetCurrentRolesAsync(bearer,ct) ?? [];
        var isRoot=roles.Contains(IdentityRoleNames.Level1);
        var isPartner=roles.Contains(IdentityRoleNames.Level2);
        if(!isRoot && !isPartner) throw new UnauthorizedAccessException("Admin required.");
        if(!await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct))
            throw new UnauthorizedAccessException("Active MFA required.");
        Guid root;
        if(isRoot) root=actor;
        else
        {
            var roots=await db.CustomerCommercialOwners.AsNoTracking()
                .Where(x=>x.PartnerLevel2UserId==actor)
                .Select(x=>x.RootLevel1UserId).Distinct().Take(2).ToArrayAsync(ct);
            if(roots.Length!=1)throw new InvalidOperationException("Partner root scope ambiguous.");
            root=roots[0];
        }
        var version=new SubscriptionPriceVersion(Guid.NewGuid(),planId,root,
            isRoot?null:actor,isRoot?PriceAudience.RootDirect:PriceAudience.Partner,
            priceIrr,fromUtc,untilUtc);
        return await catalog.AddPriceAsync(version,ct);
    }

    public async Task<StoredSubscriptionPlan> ActivatePlanAsync(string? bearer,Guid planId,
        CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles=await identity.GetCurrentRolesAsync(bearer,ct) ?? [];
        if(!roles.Contains(IdentityRoleNames.Level1) ||
            !await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
                db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct))
            throw new UnauthorizedAccessException("Only active root with MFA may activate catalog entries.");
        var plan=await db.SubscriptionPlans.SingleOrDefaultAsync(x=>x.Id==planId,ct)
            ?? throw new InvalidOperationException("Plan missing.");
        if(!await db.SubscriptionPrices.AnyAsync(p=>p.PlanId==planId && p.RootAdminUserId==actor,ct))
            throw new InvalidOperationException("Root price required before activation.");
        plan.IsActive=true;
        await db.SaveChangesAsync(ct);
        return plan;
    }
}
