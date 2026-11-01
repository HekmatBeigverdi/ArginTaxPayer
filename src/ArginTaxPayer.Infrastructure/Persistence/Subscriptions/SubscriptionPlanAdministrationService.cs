using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Authenticated plan creation. Plans are globally coded with a server-derived
/// admin prefix, preventing one administrator from impersonating another's catalog.</summary>
public sealed class SubscriptionPlanAdministrationService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity,
    SubscriptionCatalogRepository catalog)
{
    public async Task<StoredSubscriptionPlan> CreateAsync(string? accessToken, string code,
        SubscriptionPlanKind kind, int validityDays, long? invoiceUnits,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(accessToken, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var isAdmin = await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor &&
            u.IsActive && (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) ||
                u.Roles.Any(r => r.Role == IdentityRoleNames.Level2)) &&
            db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null), ct);
        if (!isAdmin) throw new UnauthorizedAccessException("Active administrator with MFA required.");
        if (code is null || code.Length is < 1 or > 50 ||
            !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9_-]+$"))
            throw new ArgumentException("Invalid plan code.");
        new SubscriptionPlanTerms(Guid.NewGuid(), kind, validityDays, invoiceUnits).Validate();
        var scopedCode = $"{actor:N}-{code}";
        if (await db.SubscriptionPlans.AnyAsync(plan => plan.Code == scopedCode, ct))
            throw new InvalidOperationException("A plan with this code already exists.");

        try
        {
            return await catalog.AddPlanAsync(scopedCode, kind, validityDays, invoiceUnits, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_subscription_plan_catalog_code" })
        {
            // Keep duplicate-code races on the same friendly 409 path as the preflight check.
            throw new InvalidOperationException("A plan with this code already exists.", ex);
        }
    }
    private async Task<Guid> RequireAdminAsync(string? token,CancellationToken ct)
    {
        var actor=await identity.CurrentUserIdAsync(token,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        if(!await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            (u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) ||
             u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2)) &&
             db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct))
            throw new UnauthorizedAccessException("Admin MFA required.");
        return actor;
    }
    public async Task<object[]> ListAsync(string? bearer,CancellationToken ct=default)
    {
        var actor=await RequireAdminAsync(bearer,ct);
        var prefix=actor.ToString("N")+"-";
        var plans=await db.SubscriptionPlans.AsNoTracking()
            .Where(p=>p.Code.StartsWith(prefix)).OrderByDescending(p=>p.CreatedAtUtc)
            .ToArrayAsync(ct);
        return plans.Select(p=>(object)new {p.Id,Code=p.Code[prefix.Length..],p.Kind,
            p.ValidityDays,p.InvoiceUnitLimit,p.IsActive,p.CreatedAtUtc}).ToArray();
    }
    public async Task<StoredSubscriptionPlan> UpdateAsync(string? bearer,Guid id,string code,
        SubscriptionPlanKind kind,int validityDays,long? invoiceUnits,CancellationToken ct=default)
    {
        var actor=await RequireAdminAsync(bearer,ct);
        if(string.IsNullOrWhiteSpace(code) || code.Length>50 ||
           !System.Text.RegularExpressions.Regex.IsMatch(code,"^[A-Za-z0-9_-]+$"))
            throw new ArgumentException("Invalid plan code.");
        new SubscriptionPlanTerms(id,kind,validityDays,invoiceUnits).Validate();
        var plan=await db.SubscriptionPlans.SingleOrDefaultAsync(p=>p.Id==id,ct)
            ?? throw new InvalidOperationException("Plan missing.");
        if(!plan.Code.StartsWith(actor.ToString("N")+"-",StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Plan outside admin scope.");
        if(await db.SubscriptionPrices.AnyAsync(x=>x.PlanId==id,ct) ||
           await db.CustomerSubscriptions.AnyAsync(x=>x.PlanId==id,ct))
            throw new InvalidOperationException("Referenced plans are immutable; create a replacement.");
        plan.Code=actor.ToString("N")+"-"+code;
        plan.Kind=kind;plan.ValidityDays=validityDays;plan.InvoiceUnitLimit=invoiceUnits;
        await db.SaveChangesAsync(ct);
        return plan;
    }
    public async Task DeactivateAsync(string? bearer,Guid id,CancellationToken ct=default)
    {
        var actor=await RequireAdminAsync(bearer,ct);
        var plan=await db.SubscriptionPlans.SingleOrDefaultAsync(p=>p.Id==id,ct)
            ?? throw new InvalidOperationException("Plan missing.");
        if(!plan.Code.StartsWith(actor.ToString("N")+"-",StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Plan outside admin scope.");
        // Archive only: never delete contractual price or subscription history.
        plan.IsActive=false;
        await db.SaveChangesAsync(ct);
    }

}
