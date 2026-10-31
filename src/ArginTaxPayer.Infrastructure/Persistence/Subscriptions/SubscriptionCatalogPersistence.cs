using Microsoft.EntityFrameworkCore;
using ArginTaxPayer.Domain.Subscriptions;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed class StoredSubscriptionPlan
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public SubscriptionPlanKind Kind { get; set; }
    public int ValidityDays { get; set; }
    public long? InvoiceUnitLimit { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class StoredSubscriptionPrice
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid RootAdminUserId { get; set; }
    public Guid? PartnerUserId { get; set; }
    public PriceAudience Audience { get; set; }
    public long PriceIrr { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class StoredCustomerSubscription
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid PlanId { get; set; }
    public Guid PriceVersionId { get; set; }
    public SubscriptionPlanKind Kind { get; set; }
    public int ValidityDays { get; set; }
    public long? InvoiceUnitLimit { get; set; }
    public long PriceIrr { get; set; }
    public Guid RootAdminUserId { get; set; }
    public Guid? PartnerUserId { get; set; }
    public SubscriptionLifecycle State { get; set; }
    public DateTime PurchasedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public long Revision { get; set; } = 1;
}

public static class SubscriptionCatalogConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        var plan = modelBuilder.Entity<StoredSubscriptionPlan>();
        plan.ToTable("subscription_plan_catalog");
        plan.HasKey(x => x.Id);
        plan.Property(x => x.Id).HasColumnName("id");
        plan.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        plan.Property(x => x.Kind).HasColumnName("kind").HasConversion<int>();
        plan.Property(x => x.ValidityDays).HasColumnName("validity_days");
        plan.Property(x => x.InvoiceUnitLimit).HasColumnName("invoice_unit_limit");
        plan.Property(x => x.IsActive).HasColumnName("is_active");
        plan.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        plan.HasIndex(x => x.Code).IsUnique();

        var price = modelBuilder.Entity<StoredSubscriptionPrice>();
        price.ToTable("subscription_price_versions");
        price.HasKey(x => x.Id);
        price.Property(x => x.Id).HasColumnName("id");
        price.Property(x => x.PlanId).HasColumnName("plan_id");
        price.Property(x => x.RootAdminUserId).HasColumnName("root_admin_user_id");
        price.Property(x => x.PartnerUserId).HasColumnName("partner_user_id");
        price.Property(x => x.Audience).HasColumnName("audience").HasConversion<int>();
        price.Property(x => x.PriceIrr).HasColumnName("price_irr");
        price.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").HasColumnType("timestamp with time zone");
        price.Property(x => x.EffectiveUntilUtc).HasColumnName("effective_until_utc").HasColumnType("timestamp with time zone");
        price.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        price.HasOne<StoredSubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        price.HasIndex(x => new { x.RootAdminUserId, x.PartnerUserId, x.PlanId, x.EffectiveFromUtc });

        var sub = modelBuilder.Entity<StoredCustomerSubscription>();
        sub.ToTable("customer_subscriptions");
        sub.HasKey(x => x.Id);
        sub.Property(x => x.Id).HasColumnName("id");
        sub.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        sub.Property(x => x.PlanId).HasColumnName("plan_id");
        sub.Property(x => x.PriceVersionId).HasColumnName("price_version_id");
        sub.Property(x => x.Kind).HasColumnName("kind").HasConversion<int>();
        sub.Property(x => x.ValidityDays).HasColumnName("validity_days");
        sub.Property(x => x.InvoiceUnitLimit).HasColumnName("invoice_unit_limit");
        sub.Property(x => x.PriceIrr).HasColumnName("price_irr");
        sub.Property(x => x.RootAdminUserId).HasColumnName("root_admin_user_id");
        sub.Property(x => x.PartnerUserId).HasColumnName("partner_user_id");
        sub.Property(x => x.State).HasColumnName("state").HasConversion<int>();
        sub.Property(x => x.PurchasedAtUtc).HasColumnName("purchased_at_utc").HasColumnType("timestamp with time zone");
        sub.Property(x => x.ActivatedAtUtc).HasColumnName("activated_at_utc").HasColumnType("timestamp with time zone");
        sub.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
        sub.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        sub.HasOne<StoredSubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        sub.HasOne<StoredSubscriptionPrice>().WithMany().HasForeignKey(x => x.PriceVersionId).OnDelete(DeleteBehavior.Restrict);
        sub.HasIndex(x => new { x.CustomerUserId, x.PurchasedAtUtc });
    }
}

/// <summary>
/// Server-side catalog foundation. No payment callback, entitlement activation or
/// invoice-dispatch integration is performed here. Caller is responsible for role
/// authorization; pricing remains rooted in a specific commercial owner.
/// </summary>
public sealed class SubscriptionCatalogRepository(ArginTaxPayerDbContext db)
{
    public async Task<StoredSubscriptionPlan> AddPlanAsync(
        string code, SubscriptionPlanKind kind, int validityDays, long? unitLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 80) throw new ArgumentException("Invalid code.");
        var terms = new SubscriptionPlanTerms(Guid.NewGuid(), kind, validityDays, unitLimit);
        terms.Validate();
        var result = new StoredSubscriptionPlan {
            Id = terms.PlanId, Code = code.Trim(), Kind = kind, ValidityDays = validityDays,
            InvoiceUnitLimit = unitLimit, CreatedAtUtc = DateTime.UtcNow, IsActive = false
        };
        db.Set<StoredSubscriptionPlan>().Add(result);
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<StoredSubscriptionPrice> AddPriceAsync(
        SubscriptionPriceVersion version, CancellationToken cancellationToken = default)
    {
        version.Validate();
        var plan = await db.Set<StoredSubscriptionPlan>().SingleOrDefaultAsync(
            x => x.Id == version.PlanId, cancellationToken);
        if (plan is null) throw new InvalidOperationException("Plan not found.");
        var overlaps = await db.Set<StoredSubscriptionPrice>().AnyAsync(x =>
            x.PlanId == version.PlanId && x.RootAdminUserId == version.RootAdminUserId &&
            x.PartnerUserId == version.PartnerUserId && x.Audience == version.Audience &&
            (x.EffectiveUntilUtc == null || x.EffectiveUntilUtc > version.EffectiveFromUtc) &&
            (version.EffectiveUntilUtc == null || x.EffectiveFromUtc < version.EffectiveUntilUtc),
            cancellationToken);
        if (overlaps) throw new InvalidOperationException("Overlapping price window.");
        var item = new StoredSubscriptionPrice {
            Id = version.Id, PlanId = version.PlanId, RootAdminUserId = version.RootAdminUserId,
            PartnerUserId = version.PartnerUserId, Audience = version.Audience,
            PriceIrr = version.PriceIrr, EffectiveFromUtc = version.EffectiveFromUtc,
            EffectiveUntilUtc = version.EffectiveUntilUtc, CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<StoredSubscriptionPrice>().Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<SubscriptionPurchaseSnapshot> QuoteAsync(
        Guid customerId, Guid planId, Guid rootId, Guid? partnerId, DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty || rootId == Guid.Empty || atUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid quote scope.");
        var plan = await db.Set<StoredSubscriptionPlan>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == planId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Plan inactive or absent.");
        var prices = await db.Set<StoredSubscriptionPrice>().AsNoTracking().Where(x =>
            x.PlanId == planId && x.RootAdminUserId == rootId && x.PartnerUserId == partnerId &&
            x.EffectiveFromUtc <= atUtc &&
            (x.EffectiveUntilUtc == null || x.EffectiveUntilUtc > atUtc)).ToListAsync(cancellationToken);
        if (prices.Count != 1) throw new InvalidOperationException("Expected exactly one effective price.");
        var p = prices[0];
        return SubscriptionPurchaseSnapshot.Create(customerId,
            new SubscriptionPlanTerms(plan.Id, plan.Kind, plan.ValidityDays, plan.InvoiceUnitLimit),
            new SubscriptionPriceVersion(p.Id, p.PlanId, p.RootAdminUserId,
                p.PartnerUserId, p.Audience, p.PriceIrr, p.EffectiveFromUtc, p.EffectiveUntilUtc), atUtc);
    }
}
