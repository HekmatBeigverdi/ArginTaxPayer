using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record PartnerSettlementBalanceRow(Guid AllocationId, Guid RootUserId,
    Guid PartnerUserId, long EarnedIrr, long SettledIrr, long OutstandingIrr);
public sealed record PartnerSettlementBalanceReport(
    PartnerSettlementBalanceRow[] Items, long TotalEarnedIrr,
    long TotalSettledIrr, long TotalOutstandingIrr);

/// <summary>Scope before aggregation: Level1 sees root's partners, Level2 only itself.</summary>
public sealed class PartnerSettlementReadService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public async Task<PartnerSettlementBalanceReport> GetBalancesAsync(string? bearer,
        CancellationToken ct=default)
    {
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
            SELECT a.id AS "AllocationId",a.root_user_id AS "RootUserId",
              a.partner_user_id AS "PartnerUserId",a.partner_share_irr AS "EarnedIrr",
              COALESCE(SUM(CASE WHEN e.kind=1 THEN e.amount_irr
                  WHEN e.kind=2 THEN -e.amount_irr ELSE 0 END),0) AS "SettledIrr"
            FROM commercial_revenue_allocations a
            LEFT JOIN partner_settlement_events e ON e.allocation_id=a.id
            WHERE a.partner_user_id IS NOT NULL AND
                  (({0} AND a.root_user_id={1}) OR ({2} AND a.partner_user_id={1}))
            GROUP BY a.id,a.root_user_id,a.partner_user_id,a.partner_share_irr
            """,root,actor,!root && partner).ToListAsync(ct);
        var mapped=rows.Select(r=>
        {
            var position=new PartnerSettlementPosition(r.EarnedIrr,r.SettledIrr,0);
            position.Validate();
            return new PartnerSettlementBalanceRow(r.AllocationId,r.RootUserId,r.PartnerUserId,
                r.EarnedIrr,r.SettledIrr,position.OutstandingIrr);
        }).ToArray();
        return new PartnerSettlementBalanceReport(mapped,
            mapped.Sum(x=>x.EarnedIrr),mapped.Sum(x=>x.SettledIrr),mapped.Sum(x=>x.OutstandingIrr));
    }

    private sealed class PositionRow
    {
        public Guid AllocationId {get;set;}
        public Guid RootUserId {get;set;}
        public Guid PartnerUserId {get;set;}
        public long EarnedIrr {get;set;}
        public long SettledIrr {get;set;}
    }
}
