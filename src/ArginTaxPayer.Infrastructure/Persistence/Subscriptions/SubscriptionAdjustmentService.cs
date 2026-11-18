using System.Data;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record ActiveSubscriptionAdjustmentView(Guid SubscriptionId, Guid CustomerUserId,
    SubscriptionPlanKind Kind, int ValidityDays, long? InvoiceUnitLimit, long? AvailableUnits,
    long? ConsumedUnits, DateTime? ExpiresAtUtc, SubscriptionLifecycle State, long Revision,
    long PriceIrr, Guid? PartnerUserId);
public sealed record SubscriptionCancellationResult(long OriginalPriceIrr, long DeductedIrr, long RefundedIrr,
    int? RemainingDays, long? RemainingUnits, bool FullRefund);
public sealed record SubscriptionAdjustmentHistoryView(Guid Id, SubscriptionAdjustmentKind Kind,
    int? PreviousValidityDays, int? NewValidityDays, long? PreviousInvoiceUnitLimit,
    long? NewInvoiceUnitLimit, long NegativeGrossIrr, string Reason, Guid ActorUserId,
    DateTime CreatedAtUtc);

public sealed class SubscriptionAdjustmentService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public async Task<ActiveSubscriptionAdjustmentView?> ReadActiveAsync(string? token, Guid customerId,
        CancellationToken ct = default)
    {
        await RequireLevel1Async(token, ct);
        var sub = await db.CustomerSubscriptions.AsNoTracking().Where(x => x.CustomerUserId == customerId &&
            x.State == SubscriptionLifecycle.Active).OrderByDescending(x => x.ActivatedAtUtc).FirstOrDefaultAsync(ct);
        return sub is null ? null : await ViewAsync(sub, ct);
    }

    public async Task<SubscriptionAdjustmentHistoryView[]> ReadHistoryAsync(string? token, Guid customerId,
        CancellationToken ct = default)
    {
        await RequireLevel1Async(token, ct);
        return await db.SubscriptionAdjustments.AsNoTracking()
            .Where(x => x.CustomerUserId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Take(100)
            .Select(x => new SubscriptionAdjustmentHistoryView(x.Id, x.Kind,
                x.PreviousValidityDays, x.NewValidityDays, x.PreviousInvoiceUnitLimit,
                x.NewInvoiceUnitLimit, x.NegativeGrossIrr, x.Reason, x.ActorUserId, x.CreatedAtUtc))
            .ToArrayAsync(ct);
    }

    public async Task<SubscriptionCancellationResult> PreviewCancelAsync(string? token, Guid customerId,
        Guid subscriptionId, long expectedRevision, bool fullRefund, CancellationToken ct = default)
    {
        await RequireLevel1Async(token, ct);
        var sub = await db.CustomerSubscriptions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == subscriptionId &&
            x.CustomerUserId == customerId && x.State == SubscriptionLifecycle.Active, ct)
            ?? throw new KeyNotFoundException("Active subscription not found.");
        if (sub.Revision != expectedRevision) throw new InvalidOperationException("Subscription revision changed.");
        return await CalculateCancellationAsync(sub, fullRefund, ct);
    }

    public async Task<ActiveSubscriptionAdjustmentView> AdjustAsync(string? token, Guid customerId,
        Guid subscriptionId, int? validityDays, long? invoiceUnits, long expectedRevision,
        string operationKey, string reason, CancellationToken ct = default)
    {
        var actor = await RequireLevel1Async(token, ct);
        ValidateCommon(subscriptionId, expectedRevision, operationKey, reason);
        if (validityDays is <= 0 or > 366 || invoiceUnits is <= 0)
            throw new ArgumentException("Invalid adjustment value.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var sub = await db.CustomerSubscriptions.SingleOrDefaultAsync(x => x.Id == subscriptionId &&
            x.CustomerUserId == customerId && x.State == SubscriptionLifecycle.Active, ct)
            ?? throw new KeyNotFoundException("Active subscription not found.");
        var prior = await db.SubscriptionAdjustments.FirstOrDefaultAsync(x => x.OperationKey == operationKey, ct);
        if (prior is not null) { await tx.CommitAsync(ct); return await ViewByIdAsync(prior.SubscriptionId, ct); }
        if (sub.Revision != expectedRevision) throw new InvalidOperationException("Subscription revision changed.");
        var quantity = sub.Kind == SubscriptionPlanKind.Quantity;
        var oldValidity = sub.ValidityDays;
        var oldUnits = sub.InvoiceUnitLimit;
        if (quantity != (invoiceUnits is not null) || !quantity && validityDays is null)
            throw new ArgumentException("Adjustment value does not match plan kind.");
        if (quantity)
        {
            var account = await db.Database.SqlQueryRaw<CreditRow>("""
                SELECT granted_units AS "Granted",available_units AS "Available",reserved_units AS "Reserved",consumed_units AS "Consumed"
                FROM subscription_credit_accounts WHERE subscription_id={0} AND customer_user_id={1}
                """, subscriptionId, customerId).SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Credit account missing.");
            if (invoiceUnits!.Value < account.Consumed + account.Reserved)
                throw new InvalidOperationException("New quantity is below used or reserved invoices.");
            var delta = invoiceUnits.Value - account.Granted;
            if (delta != 0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE subscription_credit_accounts SET granted_units=granted_units+{delta}, available_units=available_units+{delta}, revision=revision+1
                    WHERE subscription_id={subscriptionId} AND customer_user_id={customerId}
                    """, ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO subscription_credit_entries(id,subscription_id,customer_user_id,operation_key,operation_type,reservation_key,units,recorded_at_utc)
                    VALUES({Guid.NewGuid()},{subscriptionId},{customerId},{operationKey},'admin_adjustment',NULL,{Math.Abs(delta)},{DateTime.UtcNow})
                    """, ct);
            }
            sub.InvoiceUnitLimit = invoiceUnits;
        }
        else
        {
            sub.ValidityDays = validityDays!.Value;
            sub.ExpiresAtUtc = DateTime.UtcNow.AddDays(validityDays.Value);
        }
        sub.Revision++;
        db.SubscriptionAdjustments.Add(new StoredSubscriptionAdjustment {
            Id=Guid.NewGuid(), CustomerUserId=customerId, SubscriptionId=subscriptionId, ActorUserId=actor,
            OperationKey=operationKey, Kind=SubscriptionAdjustmentKind.Revision,
            PreviousValidityDays=quantity?null:oldValidity, NewValidityDays=validityDays,
            PreviousInvoiceUnitLimit=quantity?oldUnits:null, NewInvoiceUnitLimit=invoiceUnits,
            Reason=reason.Trim(), CreatedAtUtc=DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await ViewByIdAsync(subscriptionId, ct);
    }

    public async Task<SubscriptionCancellationResult> CancelAsync(string? token, Guid customerId, Guid subscriptionId,
        long expectedRevision, string operationKey, string reason, bool fullRefund = false, CancellationToken ct = default)
    {
        var actor = await RequireLevel1Async(token, ct);
        ValidateCommon(subscriptionId, expectedRevision, operationKey, reason);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var sub = await db.CustomerSubscriptions.SingleOrDefaultAsync(x => x.Id == subscriptionId &&
            x.CustomerUserId == customerId && x.State == SubscriptionLifecycle.Active, ct)
            ?? throw new KeyNotFoundException("Active subscription not found.");
        var prior = await db.SubscriptionAdjustments.AsNoTracking().SingleOrDefaultAsync(x => x.OperationKey == operationKey, ct);
        if (prior is not null)
        {
            await tx.CommitAsync(ct);
            return new(prior.NegativeGrossIrr, 0, prior.NegativeGrossIrr,
                prior.PreviousValidityDays is null ? null : prior.PreviousValidityDays,
                prior.PreviousInvoiceUnitLimit is null ? null : prior.PreviousInvoiceUnitLimit, false);
        }
        if (sub.Revision != expectedRevision) throw new InvalidOperationException("Subscription revision changed.");
        var now = DateTime.UtcNow;
        var preview = await CalculateCancellationAsync(sub, fullRefund, ct);
        long? remainingUnits = preview.RemainingUnits;
        int? remainingDays = preview.RemainingDays;
        if (sub.Kind == SubscriptionPlanKind.Quantity)
        {
            var account = await db.Database.SqlQueryRaw<CreditRow>("""
                SELECT granted_units AS "Granted",available_units AS "Available",reserved_units AS "Reserved",consumed_units AS "Consumed"
                FROM subscription_credit_accounts WHERE subscription_id={0} AND customer_user_id={1}
                """, subscriptionId, customerId).SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Credit account missing.");
            remainingUnits = Math.Max(0, account.Available + account.Reserved);
            if (remainingUnits > 0)
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE subscription_credit_accounts SET available_units=0,reserved_units=0,revision=revision+1
                    WHERE subscription_id={subscriptionId} AND customer_user_id={customerId}
                    """, ct);
        }
        else
        {
            remainingDays = sub.ExpiresAtUtc is { } expiry
                ? Math.Max(0, (int)Math.Ceiling((expiry - now).TotalDays)) : 0;
        }
        var gross = preview.RefundedIrr;
        var basis = sub.PartnerUserId is null ? 0 : await db.Database.SqlQueryRaw<int?>("""
            SELECT partner_basis_points FROM partner_share_terms
            WHERE root_user_id={0} AND partner_user_id={1} AND effective_from_utc<=now()
              AND (effective_until_utc IS NULL OR effective_until_utc>now())
            ORDER BY effective_from_utc DESC LIMIT 1
            """, sub.RootAdminUserId, sub.PartnerUserId).SingleOrDefaultAsync(ct) ?? 0;
        var partner = sub.PartnerUserId is null ? 0 : (long)Math.Floor((decimal)gross * basis / 10000m);
        var root = gross - partner;
        sub.State = SubscriptionLifecycle.Cancelled; sub.ExpiresAtUtc = now; sub.Revision++;
        db.SubscriptionAdjustments.Add(new StoredSubscriptionAdjustment { Id=Guid.NewGuid(),CustomerUserId=customerId,
            SubscriptionId=subscriptionId,ActorUserId=actor,OperationKey=operationKey,Kind=SubscriptionAdjustmentKind.Cancellation,
            PreviousValidityDays=sub.ValidityDays,NewValidityDays=0,PreviousInvoiceUnitLimit=sub.InvoiceUnitLimit,
            NewInvoiceUnitLimit=0,NegativeGrossIrr=gross,NegativePartnerShareIrr=partner,NegativeRootShareIrr=root,
            Reason=reason.Trim(),CreatedAtUtc=DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return preview;
    }

    private async Task<SubscriptionCancellationResult> CalculateCancellationAsync(
        StoredCustomerSubscription sub, bool fullRefund, CancellationToken ct)
    {
        long? remainingUnits = null;
        int? remainingDays = null;
        if (sub.Kind == SubscriptionPlanKind.Quantity)
        {
            var account = await db.Database.SqlQueryRaw<CreditRow>("""
                SELECT granted_units AS "Granted",available_units AS "Available",reserved_units AS "Reserved",consumed_units AS "Consumed"
                FROM subscription_credit_accounts WHERE subscription_id={0} AND customer_user_id={1}
                """, sub.Id, sub.CustomerUserId).SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Credit account missing.");
            remainingUnits = Math.Max(0, account.Available + account.Reserved);
        }
        else
        {
            remainingDays = sub.ExpiresAtUtc is { } expiry
                ? Math.Max(0, (int)Math.Ceiling((expiry - DateTime.UtcNow).TotalDays)) : 0;
        }
        var prorated = sub.Kind == SubscriptionPlanKind.Quantity
            ? Prorate(sub.PriceIrr, remainingUnits!.Value, sub.InvoiceUnitLimit ?? 0)
            : Prorate(sub.PriceIrr, remainingDays!.Value, Math.Max(1, sub.ValidityDays));
        var gross = fullRefund ? sub.PriceIrr : prorated;
        return new(sub.PriceIrr, Math.Max(0, sub.PriceIrr - gross), gross, remainingDays, remainingUnits, fullRefund);
    }

    private async Task<ActiveSubscriptionAdjustmentView> ViewByIdAsync(Guid id, CancellationToken ct)
        => await db.CustomerSubscriptions.AsNoTracking().SingleAsync(x=>x.Id==id,ct) is { } s ? await ViewAsync(s,ct) : throw new KeyNotFoundException();
    private async Task<ActiveSubscriptionAdjustmentView> ViewAsync(StoredCustomerSubscription s, CancellationToken ct)
    {
        var credit= s.Kind==SubscriptionPlanKind.Quantity ? await db.Database.SqlQueryRaw<CreditRow>("SELECT available_units AS \"Available\",consumed_units AS \"Consumed\",granted_units AS \"Granted\",reserved_units AS \"Reserved\" FROM subscription_credit_accounts WHERE subscription_id={0}",s.Id).SingleOrDefaultAsync(ct):null;
        return new(s.Id,s.CustomerUserId,s.Kind,s.ValidityDays,s.InvoiceUnitLimit,credit?.Available,credit?.Consumed,s.ExpiresAtUtc,s.State,s.Revision,s.PriceIrr,s.PartnerUserId);
    }
    private async Task<Guid> RequireLevel1Async(string? token,CancellationToken ct)
    {
        var actor=await identity.CurrentUserIdAsync(token,ct)??throw new UnauthorizedAccessException();
        if(!await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor&&u.IsActive&&u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1)&&db.IdentityMfaStates.Any(m=>m.UserId==actor&&m.EnabledAtUtc!=null),ct))throw new UnauthorizedAccessException();
        return actor;
    }
    private static void ValidateCommon(Guid id,long revision,string key,string reason){if(id==Guid.Empty||revision<1||string.IsNullOrWhiteSpace(key)||key.Length>160||string.IsNullOrWhiteSpace(reason)||reason.Length>1000)throw new ArgumentException("Invalid adjustment request.");}
    private static long Prorate(long amount,long remaining,long total)
        => total <= 0 || remaining <= 0 ? 0 : (long)Math.Floor((decimal)amount * remaining / total);
    private sealed class CreditRow { public long Granted{get;set;} public long Available{get;set;} public long Reserved{get;set;} public long Consumed{get;set;} }
}
