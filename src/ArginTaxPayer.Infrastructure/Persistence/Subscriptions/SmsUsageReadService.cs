using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record SmsUsageItem(Guid DispatchId, string Purpose, Guid? CustomerUserId,
    string? CustomerContact, Guid? PartnerLevel2UserId, long ProviderCostIrr, long CustomerChargeIrr,
    DateTime RecordedAtUtc, string Status);
public sealed record SmsUsagePage(IReadOnlyList<SmsUsageItem> Items, int Page,
    int PageSize, long TotalCount, long ProviderCostTotalIrr, long CustomerChargeTotalIrr);

/// <summary>Read-only reporting over immutable SMS cost and charge evidence.</summary>
public sealed class SmsUsageReadService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<SmsUsagePage> ReadAsync(string? bearer, Guid? partnerId, int page,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles = await db.IdentityUsers.AsNoTracking().Where(x => x.Id == actor && x.IsActive)
            .Select(x => new { L1 = x.Roles.Any(r => r.Role == IdentityRoleNames.Level1),
                L2 = x.Roles.Any(r => r.Role == IdentityRoleNames.Level2) }).SingleOrDefaultAsync(ct);
        if (roles is null || (!roles.L1 && !roles.L2)) throw new UnauthorizedAccessException();
        var partnerScoped = roles.L2 && !roles.L1;
        if (partnerScoped) partnerId = actor;
        page = Math.Clamp(page, 1, 1_000_000);
        const int pageSize = 10;
        var partnerClause = !partnerScoped && partnerId.HasValue ? " AND d.partner_level2_user_id={1}" : "";
        var scopeClause = partnerScoped ? " AND d.partner_level2_user_id={0}" : " AND d.root_level1_user_id={0}";
        var countSql = "SELECT count(*) AS \"Value\" FROM sms_provider_dispatches d WHERE d.purpose IN ('register','login','reset','notification')" + scopeClause + partnerClause;
        var args = !partnerScoped && partnerId.HasValue ? new object[] { actor, partnerId.Value } : new object[] { actor };
        var count = await db.Database.SqlQueryRaw<long>(countSql, args).SingleAsync(ct);
        var offset = (page - 1) * pageSize;
        var rows = await db.Database.SqlQueryRaw<SmsUsageRow>("""
            SELECT d.id AS "DispatchId", d.purpose AS "Purpose", COALESCE(d.sms_user_id,l.customer_user_id) AS "CustomerUserId",
              COALESCE(u.normalized_email,u.normalized_mobile) AS "CustomerContact",
              d.partner_level2_user_id AS "PartnerLevel2UserId",
              COALESCE(e.provider_cost_irr,0)::bigint AS "ProviderCostIrr",
              COALESCE(c.customer_charge_irr,0)::bigint AS "CustomerChargeIrr",
              COALESCE(e.recorded_at_utc,d.created_at_utc) AS "RecordedAtUtc",
              CASE d.state WHEN 1 THEN 'pending' WHEN 2 THEN 'accepted' WHEN 3 THEN 'ambiguous'
                WHEN 4 THEN 'rejected' WHEN 5 THEN 'settled' WHEN 6 THEN 'reconciliation' ELSE 'local' END AS "Status"
            FROM sms_provider_dispatches d
            LEFT JOIN sms_dispatch_customer_links l ON l.dispatch_id=d.id
            LEFT JOIN identity_users u ON u.id=COALESCE(d.sms_user_id,l.customer_user_id)
            LEFT JOIN sms_provider_expense_events e ON e.dispatch_id=d.id
            LEFT JOIN sms_customer_sms_charges c ON c.dispatch_id=d.id
            WHERE d.purpose IN ('register','login','reset','notification')
            """ + scopeClause + partnerClause + " ORDER BY d.created_at_utc DESC,d.id DESC LIMIT 10 OFFSET " + offset,
            args).ToListAsync(ct);
        var totals = await db.Database.SqlQueryRaw<SmsTotals>("""
            SELECT COALESCE(sum(e.provider_cost_irr),0)::bigint AS "ProviderCostIrr",
              COALESCE(sum(c.customer_charge_irr),0)::bigint AS "CustomerChargeIrr"
            FROM sms_provider_dispatches d
            LEFT JOIN sms_provider_expense_events e ON e.dispatch_id=d.id
            LEFT JOIN sms_customer_sms_charges c ON c.dispatch_id=d.id
            WHERE d.purpose IN ('register','login','reset','notification')
            """ + scopeClause + partnerClause, args).SingleAsync(ct);
        return new SmsUsagePage(rows.Select(x => new SmsUsageItem(x.DispatchId,x.Purpose,
            x.CustomerUserId,x.CustomerContact,x.PartnerLevel2UserId,x.ProviderCostIrr,x.CustomerChargeIrr,
            x.RecordedAtUtc,x.Status)).ToArray(),page,pageSize,count,totals.ProviderCostIrr,totals.CustomerChargeIrr);
    }
    private sealed class SmsUsageRow { public Guid DispatchId {get;set;} public string Purpose {get;set;}=""; public Guid? CustomerUserId {get;set;} public string? CustomerContact {get;set;} public Guid? PartnerLevel2UserId {get;set;} public long ProviderCostIrr {get;set;} public long CustomerChargeIrr {get;set;} public DateTime RecordedAtUtc {get;set;} public string Status {get;set;}=""; }
    private sealed class SmsTotals { public long ProviderCostIrr {get;set;} public long CustomerChargeIrr {get;set;} }
}
