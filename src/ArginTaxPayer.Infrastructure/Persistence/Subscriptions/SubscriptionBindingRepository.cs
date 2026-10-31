using ArginTaxPayer.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>
/// Internal persistence boundary only. No public endpoint is exposed until
/// a verified authority-evidence adapter and role authorization are wired.
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
        if (s.Kind == SubscriptionPlanKind.AnnualSingleTaxpayer)
        {
            var binding = await ReadAnnualBindingAsync(customerId, subscriptionId, ct);
            SubscriptionBindingPolicy.RequireAnnualMatch(binding, customerId, taxpayerId, memoryId);
        }
        else
        {
            var taxpayerOwned = await db.TaxpayerProfiles.AsNoTracking()
                .AnyAsync(x => x.Id == taxpayerId && x.CustomerUserId == customerId, ct);
            var memoryOwned = await db.FiscalMemories.AsNoTracking()
                .AnyAsync(x => x.Id == memoryId && x.TaxpayerId == taxpayerId, ct);
            if (!taxpayerOwned || !memoryOwned)
                throw new InvalidOperationException("Taxpayer or memory outside customer scope.");
        }
    }

    public async Task<StoredAnnualBinding> RegisterVerifiedAnnualBindingAsync(
        Guid customerId, Guid subscriptionId, Guid taxpayerId, Guid memoryId,
        DateTime verifiedAtUtc, string evidenceReference, CancellationToken ct = default)
    {
        if (verifiedAtUtc.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(evidenceReference) || evidenceReference.Length > 128)
            throw new ArgumentException("Trusted authority evidence is required.");
        // Caller MUST be a trusted server-side authority-evidence adapter, never browser input.
        var s = await db.CustomerSubscriptions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == subscriptionId && x.CustomerUserId == customerId, ct)
            ?? throw new InvalidOperationException("Subscription not found.");
        SubscriptionBindingPolicy.RequireEligible(s.Kind, s.State,
            s.ActivatedAtUtc, s.ExpiresAtUtc, verifiedAtUtc);
        if (s.Kind != SubscriptionPlanKind.AnnualSingleTaxpayer)
            throw new InvalidOperationException("Only annual plans can be permanently bound.");
        var match = await db.TaxpayerProfiles.AsNoTracking()
            .AnyAsync(x => x.Id == taxpayerId && x.CustomerUserId == customerId, ct);
        var memory = await db.FiscalMemories.AsNoTracking()
            .AnyAsync(x => x.Id == memoryId && x.TaxpayerId == taxpayerId, ct);
        if (!match || !memory) throw new InvalidOperationException("Invalid taxpayer/memory ownership.");
        var binding = new StoredAnnualBinding {
            SubscriptionId = subscriptionId, CustomerUserId = customerId,
            TaxpayerId = taxpayerId, FiscalMemoryId = memoryId,
            VerifiedConnectedAtUtc = verifiedAtUtc, EvidenceReference = evidenceReference.Trim()
        };
        db.AnnualSubscriptionBindings.Add(binding);
        await db.SaveChangesAsync(ct); // primary key + database trigger prevent rebinding
        return binding;
    }
}
