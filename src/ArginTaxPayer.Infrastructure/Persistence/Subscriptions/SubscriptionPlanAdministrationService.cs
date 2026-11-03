using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record SubscriptionPartnerPriceSummary(Guid PartnerUserId, string? Mobile, long? PriceIrr);
public sealed record SubscriptionPlanCatalogItem(Guid Id, string Code,
    SubscriptionPlanKind Kind, int ValidityDays, long? InvoiceUnitLimit, bool IsActive,
    DateTime CreatedAtUtc, bool IsOwnedByActor, long? BasePriceIrr, long? OwnPriceIrr,
    int PartnerPriceCount, string? OwnerMobile, SubscriptionPartnerPriceSummary[] PartnerPrices);

/// <summary>Authenticated plan creation. Plans are globally coded with a server-derived
/// admin prefix, preventing one administrator from impersonating another's catalog.</summary>
public sealed class SubscriptionPlanAdministrationService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity,
    SubscriptionCatalogRepository catalog, SubscriptionPriceAdministrationService priceAdministration)
{
    public Task<StoredSubscriptionPlan> CreateAsync(string? accessToken, string code,
        SubscriptionPlanKind kind, int validityDays, long? invoiceUnits,
        CancellationToken ct = default, long? initialPartnerPriceIrr = null)
    {
        var strategy=db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async()=>
        {
            db.ChangeTracker.Clear();
            return await CreateCoreAsync(accessToken,code,kind,validityDays,invoiceUnits,ct,initialPartnerPriceIrr);
        });
    }

    private async Task<StoredSubscriptionPlan> CreateCoreAsync(string? accessToken, string code,
        SubscriptionPlanKind kind, int validityDays, long? invoiceUnits,
        CancellationToken ct, long? initialPartnerPriceIrr)
    {
        var actor = await identity.CurrentUserIdAsync(accessToken, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var actorRoles = await identity.GetCurrentRolesAsync(accessToken, ct) ?? [];
        var isRoot = actorRoles.Contains(IdentityRoleNames.Level1);
        var isPartner = actorRoles.Contains(IdentityRoleNames.Level2) && !isRoot;
        var isAdmin = await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor &&
            u.IsActive && (isRoot && u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) ||
                isPartner && u.Roles.Any(r => r.Role == IdentityRoleNames.Level2)) &&
            db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null), ct);
        if (!isAdmin) throw new UnauthorizedAccessException("Active administrator with MFA required.");
        if (isPartner && (kind != SubscriptionPlanKind.Quantity || initialPartnerPriceIrr is null))
            throw new ArgumentException("Level2 administrators must submit a quantity plan with its initial price.");
        if (isRoot && initialPartnerPriceIrr is not null)
            throw new ArgumentException("Root administrators create base plans without a partner price.");
        if (code is null || code.Length is < 1 or > 50 ||
            !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9_-]+$"))
            throw new ArgumentException("Invalid plan code.");
        new SubscriptionPlanTerms(Guid.NewGuid(), kind, validityDays, invoiceUnits).Validate();
        var scopedCode = $"{actor:N}-{code}";
        if (await db.SubscriptionPlans.AnyAsync(plan => plan.Code == scopedCode, ct))
            throw new InvalidOperationException("A plan with this code already exists.");

        await using var transaction = isPartner
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        try
        {
            var plan = await catalog.AddPlanAsync(scopedCode, kind, validityDays, invoiceUnits, ct);
            if (initialPartnerPriceIrr is { } initialPrice)
                await priceAdministration.CreateAsync(accessToken, plan.Id, initialPrice,
                    DateTime.UtcNow, null, ct, replaceCurrent: false);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return plan;
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
        => (await ListCoreAsync(bearer,null,null,ct)).Items;

    public Task<SubscriptionPage<object>> ListPageAsync(string? bearer,int page=1,
        string? search=null,CancellationToken ct=default)
        => ListCoreAsync(bearer,page,search,ct);

    internal static IQueryable<StoredSubscriptionPlan> CatalogQuery(
        IQueryable<StoredSubscriptionPlan> source,Guid actor,string[] prefixes,
        bool isPartner,string? search)
    {
        var actorPrefix=actor.ToString("N")+"-";
        var query=source.Where(p=>prefixes.Contains(p.Code.Substring(0,33)) &&
            (!isPartner || p.Code.StartsWith(actorPrefix) || p.IsActive));
        if(!string.IsNullOrWhiteSpace(search))
        {
            var term=search.Trim().ToLowerInvariant();
            var kinds=new (SubscriptionPlanKind Kind,string Label)[]{
                (SubscriptionPlanKind.AnnualSingleTaxpayer,"سالانه"),
                (SubscriptionPlanKind.Quantity,"تعدادی"),
                (SubscriptionPlanKind.MonthlySingleTaxpayer,"ماهانه"),
                (SubscriptionPlanKind.QuarterlySingleTaxpayer,"سه‌ماهه"),
                (SubscriptionPlanKind.SemiAnnualSingleTaxpayer,"شش‌ماهه")}
                .Where(x=>x.Label.Contains(term)).Select(x=>x.Kind).ToArray();
            query=query.Where(p=>p.Code.Substring(33).ToLower().Contains(term) || kinds.Contains(p.Kind));
        }
        return query;
    }

    private async Task<SubscriptionPage<object>> ListCoreAsync(string? bearer,int? page,
        string? search,CancellationToken ct)
    {
        var offset=page is { } requested ? SubscriptionPaging.Offset(requested) : 0;
        var actor=await RequireAdminAsync(bearer,ct);
        var roles=await identity.GetCurrentRolesAsync(bearer,ct) ?? [];
        var isRoot=roles.Contains(IdentityRoleNames.Level1);
        var isPartner=roles.Contains(IdentityRoleNames.Level2) && !isRoot;
        var root=actor;
        Guid[] partnerIds=[];
        if(isPartner)
        {
            var roots=await db.CustomerCommercialOwners.AsNoTracking()
                .Where(x=>x.PartnerLevel2UserId==actor).Select(x=>x.RootLevel1UserId)
                .Distinct().Take(2).ToArrayAsync(ct);
            if(roots.Length!=1)throw new InvalidOperationException("Partner root scope ambiguous.");
            root=roots[0];
        }
        else partnerIds=await db.CustomerCommercialOwners.AsNoTracking()
            .Where(x=>x.RootLevel1UserId==actor && x.PartnerLevel2UserId!=null)
            .Select(x=>x.PartnerLevel2UserId!.Value).Distinct().ToArrayAsync(ct);

        var prefixes=new[]{actor.ToString("N")+"-",root.ToString("N")+"-"}
            .Concat(partnerIds.Select(x=>x.ToString("N")+"-" )).Distinct().ToArray();
        var query=CatalogQuery(db.SubscriptionPlans.AsNoTracking(),actor,prefixes,isPartner,search);
        var totalCount=await query.CountAsync(ct);
        var ordered=query.OrderByDescending(p=>p.CreatedAtUtc).ThenBy(p=>p.Id);
        var plans=await (page.HasValue ? ordered.Skip(offset).Take(SubscriptionPaging.PageSize) : ordered)
            .ToArrayAsync(ct);
        var planIds=plans.Select(p=>p.Id).ToArray();
        var now=DateTime.UtcNow;
        var prices=await db.SubscriptionPrices.AsNoTracking().Where(p=>planIds.Contains(p.PlanId) &&
            p.RootAdminUserId==root && p.EffectiveFromUtc<=now &&
            (p.EffectiveUntilUtc==null || p.EffectiveUntilUtc>now) &&
            (isRoot ? p.PartnerUserId==null || partnerIds.Contains(p.PartnerUserId.Value)
                : p.PartnerUserId==null || p.PartnerUserId==actor)).ToArrayAsync(ct);
        var owners=plans.Where(p=>!p.Code.StartsWith(actor.ToString("N")+"-",StringComparison.Ordinal))
            .Select(p=>p.Code.Length>=32 && Guid.TryParseExact(p.Code[..32],"N",out var ownerId)?ownerId:Guid.Empty)
            .Where(x=>x!=Guid.Empty).Distinct().ToArray();
        var mobiles=await db.IdentityUsers.AsNoTracking().Where(u=>owners.Contains(u.Id))
            .Select(u=>new{u.Id,u.NormalizedMobile}).ToDictionaryAsync(u=>u.Id,u=>u.NormalizedMobile,ct);
        var partnerOwnerIds=isRoot?await db.SubscriptionPrices.AsNoTracking()
            .Where(x=>planIds.Contains(x.PlanId) && x.RootAdminUserId==root && x.PartnerUserId!=null)
            .Select(x=>x.PartnerUserId!.Value).Distinct().ToArrayAsync(ct):[];
        var partnerMobiles=await db.IdentityUsers.AsNoTracking().Where(u=>partnerOwnerIds.Contains(u.Id))
            .Select(u=>new{u.Id,u.NormalizedMobile}).ToDictionaryAsync(u=>u.Id,u=>u.NormalizedMobile,ct);
        var items=plans.Select(p=>
        {
            var rows=prices.Where(x=>x.PlanId==p.Id).ToArray();
            var planOwner=p.Code.Length>=32 && Guid.TryParseExact(p.Code[..32],"N",out var parsedOwner)?parsedOwner:Guid.Empty;
            return (object)new SubscriptionPlanCatalogItem(p.Id,
                p.Code.Length>33?p.Code[33..]:p.Code,p.Kind,p.ValidityDays,p.InvoiceUnitLimit,p.IsActive,
                p.CreatedAtUtc,p.Code.StartsWith(actor.ToString("N")+"-",StringComparison.Ordinal),
                rows.SingleOrDefault(x=>x.PartnerUserId==null)?.PriceIrr,
                isPartner?rows.SingleOrDefault(x=>x.PartnerUserId==actor)?.PriceIrr:null,
                isRoot?rows.Count(x=>x.PartnerUserId!=null):0,
                isRoot?mobiles.GetValueOrDefault(planOwner):null,
                isRoot?partnerOwnerIds.Where(partner=>partnerIds.Contains(partner))
                    .Select(partner=>new SubscriptionPartnerPriceSummary(partner,
                        partnerMobiles.GetValueOrDefault(partner),rows.SingleOrDefault(x=>x.PartnerUserId==partner)?.PriceIrr)).ToArray():[]);
        }).ToArray();
        return new SubscriptionPage<object>(items,totalCount,page ?? 1);
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
