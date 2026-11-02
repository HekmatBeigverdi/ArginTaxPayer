using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase12CatalogPagingTests
{
    [Fact]
    public void Catalog_scope_and_Persian_search_translate_before_database_paging()
    {
        using var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=unused;Password=unused").Options);
        var actor = Guid.NewGuid();
        var root = Guid.NewGuid();
        var query = SubscriptionPlanAdministrationService.CatalogQuery(db.SubscriptionPlans,
            actor, [actor.ToString("N") + "-", root.ToString("N") + "-"], true, "ماهانه");
        var sql = query.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id)
            .Skip(10).Take(10).ToQueryString();
        Assert.Contains("WHERE", sql);
        Assert.Contains("substring", sql);
        Assert.Contains("is_active", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("kind", sql);
    }

    [Fact]
    public void Search_matches_visible_code_and_kind_but_never_owner_prefix_or_outside_scope()
    {
        var actor = Guid.NewGuid();
        var root = Guid.NewGuid();
        StoredSubscriptionPlan Plan(Guid owner, string code, bool active = true,
            SubscriptionPlanKind kind = SubscriptionPlanKind.AnnualSingleTaxpayer) =>
            new() { Id = Guid.NewGuid(), Code = owner.ToString("N") + "-" + code, IsActive = active, Kind = kind };
        var own = Plan(actor, "monthly", false, SubscriptionPlanKind.MonthlySingleTaxpayer);
        var rootPlan = Plan(root, "ACTIVE");
        var rows = new[] { own, rootPlan, Plan(root, "hidden", false), Plan(Guid.NewGuid(), "outside") }.AsQueryable();
        var prefixes = new[] { actor.ToString("N") + "-", root.ToString("N") + "-" };
        Assert.Equal(2, SubscriptionPlanAdministrationService.CatalogQuery(rows, actor, prefixes, true, null).Count());
        Assert.Equal(own.Id, SubscriptionPlanAdministrationService.CatalogQuery(rows, actor, prefixes, true, "ماهانه").Single().Id);
        Assert.Equal(rootPlan.Id, SubscriptionPlanAdministrationService.CatalogQuery(rows, actor, prefixes, true, "active").Single().Id);
        Assert.Empty(SubscriptionPlanAdministrationService.CatalogQuery(rows, actor, prefixes, true, actor.ToString("N")));
    }
    [Fact]
    public void Price_history_scope_and_paging_translate_to_SQL()
    {
        using var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=unused;Password=unused").Options);
        var sql = SubscriptionPriceAdministrationService.HistoryQuery(db.SubscriptionPrices,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
            .OrderByDescending(p => p.EffectiveFromUtc).ThenBy(p => p.Id)
            .Skip(SubscriptionPaging.Offset(2)).Take(SubscriptionPaging.PageSize).ToQueryString();
        Assert.Contains("plan_id", sql);
        Assert.Contains("root_admin_user_id", sql);
        Assert.Contains("partner_user_id", sql);
        Assert.Contains("WHERE", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
    }

    [Fact]
    public void History_excludes_other_plans_roots_and_partner_scopes_before_paging()
    {
        var plan = Guid.NewGuid();
        var root = Guid.NewGuid();
        var partner = Guid.NewGuid();
        var rows = Enumerable.Range(0, 25).Select(i => new StoredSubscriptionPrice {
            Id = Guid.NewGuid(), PlanId = plan, RootAdminUserId = root, PartnerUserId = partner,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)
        }).Concat(new[] {
            new StoredSubscriptionPrice { PlanId = plan, RootAdminUserId = root },
            new StoredSubscriptionPrice { PlanId = Guid.NewGuid(), RootAdminUserId = root, PartnerUserId = partner },
            new StoredSubscriptionPrice { PlanId = plan, RootAdminUserId = Guid.NewGuid(), PartnerUserId = partner }
        }).AsQueryable();
        var query = SubscriptionPriceAdministrationService.HistoryQuery(rows, plan, root, partner);
        Assert.Equal(25, query.Count());
        var ordered = query.OrderByDescending(p => p.EffectiveFromUtc).ThenBy(p => p.Id);
        var first = ordered.Take(10).Select(p => p.Id).ToArray();
        var second = ordered.Skip(10).Take(10).Select(p => p.Id).ToArray();
        Assert.Equal(10, second.Length);
        Assert.Empty(first.Intersect(second));
        Assert.Equal(5, ordered.Skip(20).Take(10).Count());
    }

}
