using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record PartnerSettlementBalanceRow(Guid AllocationId, Guid RootUserId,
    Guid PartnerUserId, long EarnedIrr, long SettledIrr, long OutstandingIrr);
public sealed record PartnerSettlementBalanceReport(
    PartnerSettlementBalanceRow[] Items, long TotalEarnedIrr,
    long TotalSettledIrr, long TotalOutstandingIrr, int TotalCount, int Page, int PageSize);

/// <summary>Scope before aggregation: Level1 sees root's partners, Level2 only itself.</summary>
public sealed class PartnerSettlementReadService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public async Task<PartnerSettlementBalanceReport> GetBalancesAsync(string? bearer,
        CancellationToken ct=default, int page=1)
    {
        var offset=SubscriptionPaging.Offset(page);
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles=await identity.GetCurrentRolesAsync(bearer,ct) ?? [];
        bool root=roles.Contains(IdentityRoleNames.Level1);
        bool partner=roles.Contains(IdentityRoleNames.Level2);
        if(!root && !partner)throw new UnauthorizedAccessException("Administrator required.");
        var active=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive,ct);
        if(!active)throw new UnauthorizedAccessException("Inactive account.");
        var rows=await db.Database.SqlQueryRaw<PositionRow>(
            """
            WITH positions AS (
            SELECT a.id AS "AllocationId",a.root_user_id AS "RootUserId",
              a.partner_user_id AS "PartnerUserId",a.partner_share_irr AS "EarnedIrr",
              COALESCE(SUM(CASE WHEN e.kind=1 THEN e.amount_irr
                  WHEN e.kind=2 THEN -e.amount_irr ELSE 0 END),0)::bigint AS "SettledIrr"
            FROM commercial_revenue_allocations a
            LEFT JOIN partner_settlement_events e ON e.allocation_id=a.id
            WHERE a.partner_user_id IS NOT NULL AND
                  (({0} AND a.root_user_id={1}) OR ({2} AND a.partner_user_id={1}))
            GROUP BY a.id,a.root_user_id,a.partner_user_id,a.partner_share_irr
            ), totals AS (
              SELECT COUNT(*)::integer AS "TotalCount",
                COALESCE(SUM("EarnedIrr"),0)::bigint AS "TotalEarnedIrr",
                COALESCE(SUM("SettledIrr"),0)::bigint AS "TotalSettledIrr",
                COALESCE(SUM("EarnedIrr"-"SettledIrr"),0)::bigint AS "TotalOutstandingIrr"
              FROM positions
            ), selected_page AS (
              SELECT * FROM positions ORDER BY "AllocationId" LIMIT {3} OFFSET {4}
            )
            SELECT p.*,t.* FROM totals t LEFT JOIN selected_page p ON TRUE
            ORDER BY p."AllocationId"
            """,root,actor,!root && partner,SubscriptionPaging.PageSize,offset).ToListAsync(ct);
        var mapped=rows.Where(r=>r.AllocationId.HasValue).Select(r=>
        {
            var position=new PartnerSettlementPosition(r.EarnedIrr!.Value,r.SettledIrr!.Value,0);
            position.Validate();
            return new PartnerSettlementBalanceRow(r.AllocationId!.Value,r.RootUserId!.Value,r.PartnerUserId!.Value,
                r.EarnedIrr.Value,r.SettledIrr.Value,position.OutstandingIrr);
        }).ToArray();
        var totals=rows[0];
        return new PartnerSettlementBalanceReport(mapped,totals.TotalEarnedIrr,
            totals.TotalSettledIrr,totals.TotalOutstandingIrr,totals.TotalCount,page,SubscriptionPaging.PageSize);
    }

    private sealed class PositionRow
    {
        public Guid? AllocationId {get;set;}
        public Guid? RootUserId {get;set;}
        public Guid? PartnerUserId {get;set;}
        public long? EarnedIrr {get;set;}
        public long? SettledIrr {get;set;}
        public int TotalCount {get;set;}
        public long TotalEarnedIrr {get;set;}
        public long TotalSettledIrr {get;set;}
        public long TotalOutstandingIrr {get;set;}
    }
}
