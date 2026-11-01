using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using System.Data;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Root and partner own different effective price windows. Ownership is
/// resolved server side; clients cannot nominate another partner's price owner.</summary>
public sealed class SubscriptionPriceAdministrationService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity,
    SubscriptionCatalogRepository catalog)
{
    public Task<StoredSubscriptionPrice> CreateAsync(string? bearer,
        Guid planId, long priceIrr, DateTime fromUtc, DateTime? untilUtc,
        CancellationToken ct=default, bool replaceCurrent=true)
    {
        if (!replaceCurrent)
            return CreateCoreAsync(bearer,planId,priceIrr,fromUtc,untilUtc,ct,false);
        var strategy=db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async()=>
        {
            db.ChangeTracker.Clear();
            await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var result=await CreateCoreAsync(bearer,planId,priceIrr,fromUtc,untilUtc,ct,true);
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    private async Task<StoredSubscriptionPrice> CreateCoreAsync(string? bearer,
        Guid planId,long priceIrr,DateTime fromUtc,DateTime? untilUtc,
        CancellationToken ct,bool replaceCurrent)
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
        Guid[] partnerIds=[];
        if(isRoot) root=actor;
        else
        {
            var roots=await db.CustomerCommercialOwners.AsNoTracking()
                .Where(x=>x.PartnerLevel2UserId==actor)
                .Select(x=>x.RootLevel1UserId).Distinct().Take(2).ToArrayAsync(ct);
            if(roots.Length!=1)throw new InvalidOperationException("Partner root scope ambiguous.");
            root=roots[0];
        }
        var plan=await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==planId,ct)
            ??throw new KeyNotFoundException("Plan not found.");
        var owner=plan.Code.Length>=32 && Guid.TryParseExact(plan.Code[..32],"N",out var parsedOwner)
            ?parsedOwner:Guid.Empty;
        if(isRoot)
        {
            partnerIds=await db.CustomerCommercialOwners.AsNoTracking()
                .Where(x=>x.RootLevel1UserId==actor && x.PartnerLevel2UserId!=null)
                .Select(x=>x.PartnerLevel2UserId!.Value).Distinct().ToArrayAsync(ct);
            if(owner!=actor && !partnerIds.Contains(owner))
                throw new UnauthorizedAccessException("Plan outside root catalog.");
        }
        else if(owner!=root && owner!=actor)
            throw new UnauthorizedAccessException("Plan outside partner catalog.");
        else if(owner==root && !plan.IsActive)
            throw new UnauthorizedAccessException("Inactive root plans are outside partner catalog.");
        var version=new SubscriptionPriceVersion(Guid.NewGuid(),planId,root,
            isRoot?null:actor,isRoot?PriceAudience.RootDirect:PriceAudience.Partner,
            priceIrr,fromUtc,untilUtc);
        var result = replaceCurrent
            ? await catalog.ReplaceCurrentPriceAsync(version,ct)
            : await catalog.AddPriceAsync(version,ct);
        return result;
    }

    public async Task<object[]> HistoryAsync(string? bearer, Guid planId,
        bool ownPrice=false, Guid? partnerUserId=null, CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ??throw new UnauthorizedAccessException("Authentication required.");
        var roles=await identity.GetCurrentRolesAsync(bearer,ct) ?? [];
        var isRoot=roles.Contains(IdentityRoleNames.Level1);
        var isPartner=roles.Contains(IdentityRoleNames.Level2) && !isRoot;
        if(!isRoot && !isPartner)throw new UnauthorizedAccessException("Admin required.");
        if(!await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct))
            throw new UnauthorizedAccessException("Active MFA required.");
        var plan=await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==planId,ct)
            ??throw new KeyNotFoundException("Plan not found.");
        var owner=plan.Code.Length>=32 && Guid.TryParseExact(plan.Code[..32],"N",out var parsedOwner)
            ?parsedOwner:Guid.Empty;
        Guid root; Guid? partnerScope;
        if(isPartner)
        {
            var roots=await db.CustomerCommercialOwners.AsNoTracking()
                .Where(x=>x.PartnerLevel2UserId==actor).Select(x=>x.RootLevel1UserId)
                .Distinct().Take(2).ToArrayAsync(ct);
            if(roots.Length!=1)throw new InvalidOperationException("Partner root scope ambiguous.");
            root=roots[0];
            if(owner!=root && owner!=actor)throw new UnauthorizedAccessException("Plan outside partner catalog.");
            if(owner==root && !plan.IsActive)throw new UnauthorizedAccessException("Inactive root plans are outside partner catalog.");
            if(partnerUserId is not null)throw new UnauthorizedAccessException("Partner history is private.");
            partnerScope=ownPrice?actor:null;
        }
        else
        {
            root=actor;
            if(owner!=actor)
            {
                var belongs=await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(x=>
                    x.RootLevel1UserId==actor && x.PartnerLevel2UserId==owner,ct);
                if(!belongs)throw new UnauthorizedAccessException("Plan outside root catalog.");
            }
            if(ownPrice)throw new ArgumentException("Root history is not partner history.");
            partnerScope=partnerUserId;
            if(partnerScope is { } selectedPartner && !await db.CustomerCommercialOwners.AsNoTracking()
                .AnyAsync(x=>x.RootLevel1UserId==actor && x.PartnerLevel2UserId==selectedPartner,ct))
                throw new UnauthorizedAccessException("Partner outside root scope.");
        }
        var history=await db.SubscriptionPrices.AsNoTracking().Where(x=>x.PlanId==planId &&
            x.RootAdminUserId==root && x.PartnerUserId==partnerScope)
            .OrderByDescending(x=>x.EffectiveFromUtc).ToArrayAsync(ct);
        var now=DateTime.UtcNow;
        string? mobile=null;
        if(partnerScope is { } partnerId)
            mobile=await db.IdentityUsers.AsNoTracking().Where(x=>x.Id==partnerId)
                .Select(x=>x.NormalizedMobile).SingleOrDefaultAsync(ct);
        return history.Select(x=>(object)new{
            x.Id,x.PlanId,x.PriceIrr,x.Audience,x.PartnerUserId,PartnerMobile=mobile,
            x.EffectiveFromUtc,x.EffectiveUntilUtc,
            IsCurrent=x.EffectiveFromUtc<=now && (x.EffectiveUntilUtc==null || x.EffectiveUntilUtc>now),
            IsUpcoming=x.EffectiveFromUtc>now
        }).ToArray();
    }

    public Task<StoredSubscriptionPlan> ActivatePlanAsync(string? bearer,Guid planId,
        CancellationToken ct=default)
    {
        var strategy=db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async()=>
        {
            db.ChangeTracker.Clear();
            await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var plan=await ActivatePlanCoreAsync(bearer,planId,ct);
            await transaction.CommitAsync(ct);
            return plan;
        });
    }

    private async Task<StoredSubscriptionPlan> ActivatePlanCoreAsync(string? bearer,Guid planId,
        CancellationToken ct)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles=await identity.GetCurrentRolesAsync(bearer,ct) ?? [];
        if(!roles.Contains(IdentityRoleNames.Level1) ||
            !await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
                db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct))
            throw new UnauthorizedAccessException("Only active root with MFA may activate catalog entries.");
        var plan=await db.SubscriptionPlans.SingleOrDefaultAsync(x=>x.Id==planId,ct)
            ?? throw new KeyNotFoundException("Plan missing.");
        var owner=plan.Code.Length>=32 && Guid.TryParseExact(plan.Code[..32],"N",out var parsedOwner)
            ?parsedOwner:Guid.Empty;
        if(owner!=actor && !await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(x=>
            x.RootLevel1UserId==actor && x.PartnerLevel2UserId==owner,ct))
            throw new UnauthorizedAccessException("Plan outside root catalog.");
        var now=DateTime.UtcNow;
        if(!await db.SubscriptionPrices.AnyAsync(p=>p.PlanId==planId && p.RootAdminUserId==actor &&
            p.PartnerUserId==null && p.Audience==PriceAudience.RootDirect &&
            p.EffectiveFromUtc<=now && (p.EffectiveUntilUtc==null || p.EffectiveUntilUtc>now),ct))
            throw new InvalidOperationException("Root price required before activation.");
        if(plan.IsActive)throw new InvalidOperationException("Plan is already active.");
        plan.IsActive=true;
        await db.SaveChangesAsync(ct);
        return plan;
    }
}
