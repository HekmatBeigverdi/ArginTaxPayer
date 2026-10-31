using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Internal authenticated exception workflow. Approval records a decision only;
/// it never rebinds the old annual subscription or changes past entitlements.</summary>
public sealed class SubscriptionExceptionReviewService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<Guid> RequestAsync(string? accessToken, Guid subscriptionId,
        Guid proposedTaxpayerId, Guid proposedMemoryId, string reason,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(accessToken, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles = await identity.GetCurrentRolesAsync(accessToken, ct) ?? [];
        if (!roles.Contains(IdentityRoleNames.Level2))
            throw new UnauthorizedAccessException("Level 2 required.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new ArgumentException("Reason required.");
        var subscription = await db.CustomerSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == subscriptionId && x.Kind == SubscriptionPlanKind.AnnualSingleTaxpayer, ct)
            ?? throw new InvalidOperationException("Annual subscription not found.");
        var owned = await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(x =>
            x.CustomerUserId == subscription.CustomerUserId && x.PartnerLevel2UserId == actor &&
            x.RootLevel1UserId == subscription.RootAdminUserId, ct);
        var taxpayer = await db.TaxpayerProfiles.AsNoTracking().AnyAsync(x =>
            x.Id == proposedTaxpayerId && x.CustomerUserId == subscription.CustomerUserId, ct);
        var memory = await db.FiscalMemories.AsNoTracking().AnyAsync(x =>
            x.Id == proposedMemoryId && x.TaxpayerId == proposedTaxpayerId, ct);
        if (!owned || !taxpayer || !memory) throw new UnauthorizedAccessException("Customer scope denied.");
        var request = new StoredBindingException {
            Id = Guid.NewGuid(), SubscriptionId = subscriptionId, CustomerUserId = subscription.CustomerUserId,
            RequesterUserId = actor, ProposedTaxpayerId = proposedTaxpayerId,
            ProposedMemoryId = proposedMemoryId, Reason = reason.Trim(),
            Status = BindingExceptionStatus.Pending, RequestedAtUtc = DateTime.UtcNow
        };
        db.SubscriptionBindingExceptions.Add(request);
        await db.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task ReviewAsync(string? accessToken, Guid requestId, bool approve,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(accessToken, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles = await identity.GetCurrentRolesAsync(accessToken, ct) ?? [];
        if (!roles.Contains(IdentityRoleNames.Level1))
            throw new UnauthorizedAccessException("Level 1 required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var request = await db.SubscriptionBindingExceptions.SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new InvalidOperationException("Exception not found.");
        if (request.Status != BindingExceptionStatus.Pending)
            throw new InvalidOperationException("Already reviewed.");
        var owned = await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(x =>
            x.CustomerUserId == request.CustomerUserId && x.RootLevel1UserId == actor, ct);
        if (!owned) throw new UnauthorizedAccessException("Root scope denied.");
        request.Status = approve ? BindingExceptionStatus.Approved : BindingExceptionStatus.Rejected;
        request.ReviewerUserId = actor;
        request.ReviewedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // A new purchased/approved subscription is needed for any prospective change.
    }
}
