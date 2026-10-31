using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record PartnerRevenueLedgerRow(Guid AllocationId, Guid CollectionId,
    Guid RootUserId, Guid? PartnerUserId, long GrossCollectedIrr,
    long ApprovedCostIrr, long PartnerShareIrr, long RootShareIrr, DateTime PostedAtUtc);

public sealed record PartnerRevenueLedgerPage(
    PartnerRevenueLedgerRow[] Items, long TotalGrossIrr, long TotalApprovedCostIrr,
    long TotalPartnerShareIrr, long TotalRootShareIrr, int Page, int PageSize);

/// <summary>Collection-backed, read-only financial view; Level2 may read its own
/// partner allocations only, while the singleton Level1 may read the root ledger.</summary>
public sealed class PartnerRevenueLedgerReadService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public async Task<PartnerRevenueLedgerPage> ReadAsync(string? bearer,
        int page=1, int pageSize=15, CancellationToken ct=default)
    {
        if(page<1 || pageSize is <1 or >100)
            throw new ArgumentOutOfRangeException(nameof(page));
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles=await identity.GetCurrentRolesAsync(bearer,ct) ?? [];
        var root=roles.Contains(IdentityRoleNames.Level1);
        var partner=roles.Contains(IdentityRoleNames.Level2);
        if(!root && !partner)throw new UnauthorizedAccessException("Administrator required.");
        var active=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive,ct);
        if(!active)throw new UnauthorizedAccessException("Inactive account.");

        // SQL scope is applied before pagination and totals: no cross-partner leakage.
        var rows=await db.Database.SqlQueryRaw<LedgerRow>(
            """
            SELECT id AS "AllocationId",collection_id AS "CollectionId",
              root_user_id AS "RootUserId",partner_user_id AS "PartnerUserId",
              gross_collected_irr AS "GrossCollectedIrr",
              approved_cost_irr AS "ApprovedCostIrr",
              partner_share_irr AS "PartnerShareIrr",
              root_share_irr AS "RootShareIrr",
              posted_at_utc AS "PostedAtUtc"
            FROM commercial_revenue_allocations
            WHERE ({0} AND root_user_id={1}) OR ({2} AND partner_user_id={1})
            """,root,actor,!root && partner)
            .AsNoTracking().ToListAsync(ct);
        var ordered=rows.OrderByDescending(x=>x.PostedAtUtc)
            .ThenBy(x=>x.AllocationId).ToArray();
        return new PartnerRevenueLedgerPage(
            ordered.Skip((page-1)*pageSize).Take(pageSize)
                .Select(x=>new PartnerRevenueLedgerRow(x.AllocationId,x.CollectionId,
                    x.RootUserId,x.PartnerUserId,x.GrossCollectedIrr,
                    x.ApprovedCostIrr,x.PartnerShareIrr,x.RootShareIrr,x.PostedAtUtc))
                .ToArray(),
            ordered.Sum(x=>x.GrossCollectedIrr),ordered.Sum(x=>x.ApprovedCostIrr),
            ordered.Sum(x=>x.PartnerShareIrr),ordered.Sum(x=>x.RootShareIrr),
            page,pageSize);
    }

    private sealed class LedgerRow
    {
        public Guid AllocationId {get;set;}
        public Guid CollectionId {get;set;}
        public Guid RootUserId {get;set;}
        public Guid? PartnerUserId {get;set;}
        public long GrossCollectedIrr {get;set;}
        public long ApprovedCostIrr {get;set;}
        public long PartnerShareIrr {get;set;}
        public long RootShareIrr {get;set;}
        public DateTime PostedAtUtc {get;set;}
    }
}
