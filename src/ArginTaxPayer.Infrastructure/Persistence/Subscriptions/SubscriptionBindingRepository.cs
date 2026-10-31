using ArginTaxPayer.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>
/// Internal persistence boundary only. Subscription assignment is an internal commercial action, not a tax authority connection test.
/// </summary>
public sealed class SubscriptionBindingRepository(ArginTaxPayerDbContext db)
{
    public async Task<AnnualSubscriptionBinding> ReadAnnualBindingAsync(
        Guid customerId, Guid subscriptionId, CancellationToken ct = default)
    {
        var b = await db.AnnualSubscriptionBindings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SubscriptionId == subscriptionId &&
                x.CustomerUserId == customerId, ct)
            ?? throw new InvalidOperationException("Verified binding not available.");
        return new AnnualSubscriptionBinding(b.SubscriptionId, b.CustomerUserId,
            b.TaxpayerId, b.FiscalMemoryId, b.VerifiedConnectedAtUtc);
    }

    public async Task RequireEligibleBindingAsync(Guid customerId, Guid subscriptionId,
        Guid taxpayerId, Guid memoryId, DateTime nowUtc, CancellationToken ct = default)
    {
        var s = await db.CustomerSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == subscriptionId && x.CustomerUserId == customerId, ct)
            ?? throw new InvalidOperationException("Subscription not found.");
        SubscriptionBindingPolicy.RequireEligible(s.Kind, s.State,
            s.ActivatedAtUtc, s.ExpiresAtUtc, nowUtc);
        if (SubscriptionBindingPolicy.IsSingleTaxpayer(s.Kind))
        {
            var binding = await ReadAnnualBindingAsync(customerId, subscriptionId, ct);
            SubscriptionBindingPolicy.RequireAnnualMatch(binding, customerId, taxpayerId, memoryId);
        }
        // Fiscal-memory identity may change through the existing Level1-approved
        // lifecycle; never freeze it as an additional commercial plan condition.
        {

            var taxpayerOwned = await db.TaxpayerProfiles.AsNoTracking()
                .AnyAsync(x => x.Id == taxpayerId && x.CustomerUserId == customerId, ct);
            var memoryOwned = await db.FiscalMemories.AsNoTracking()
                .AnyAsync(x => x.Id == memoryId && x.TaxpayerId == taxpayerId, ct);
            if (!taxpayerOwned || !memoryOwned)
                throw new InvalidOperationException("Taxpayer or memory outside customer scope.");
        }
    }

    public async Task<StoredAnnualBinding> RegisterSingleTaxpayerBindingAsync(
        Guid customerId, Guid subscriptionId, Guid taxpayerId, Guid memoryId,
        DateTime selectedAtUtc, CancellationToken ct = default)
    {
        if (selectedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Selection timestamp must be UTC.");
        var s = await db.CustomerSubscriptions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == subscriptionId && x.CustomerUserId == customerId, ct)
            ?? throw new InvalidOperationException("Subscription not found.");
        SubscriptionBindingPolicy.RequireEligible(s.Kind, s.State,
            s.ActivatedAtUtc, s.ExpiresAtUtc, selectedAtUtc);
        if (!SubscriptionBindingPolicy.IsSingleTaxpayer(s.Kind))
            throw new InvalidOperationException("Only single-taxpayer plans can be bound.");
        var match = await db.TaxpayerProfiles.AsNoTracking()
            .AnyAsync(x => x.Id == taxpayerId && x.CustomerUserId == customerId, ct);
        var memory = await db.FiscalMemories.AsNoTracking()
            .AnyAsync(x => x.Id == memoryId && x.TaxpayerId == taxpayerId, ct);
        if (!match || !memory) throw new InvalidOperationException("Invalid taxpayer/memory ownership.");
        var binding = new StoredAnnualBinding {
            SubscriptionId = subscriptionId, CustomerUserId = customerId,
            TaxpayerId = taxpayerId, FiscalMemoryId = memoryId,
            VerifiedConnectedAtUtc = selectedAtUtc, EvidenceReference = "internal-plan-selection"
        };
        db.AnnualSubscriptionBindings.Add(binding);
        await db.SaveChangesAsync(ct); // primary key + database trigger prevent rebinding
        return binding;
    }
}
