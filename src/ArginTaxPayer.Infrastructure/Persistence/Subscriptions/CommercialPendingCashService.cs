using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Pending commercial orders for the current commercial cashier only.
/// A partner sees only partner-attributed orders; root sees direct orders only.</summary>
public sealed class CommercialPendingCashService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public sealed record PendingRow(Guid OrderId,Guid CustomerUserId,Guid PlanId,
        long GrossIrr,long CollectedIrr,DateTime IssuedAtUtc,string PlanCode);
    public async Task<PendingRow[]> ReadMineAsync(string? bearer,CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var authorized=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            (u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) ||
             u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2)),ct);
        if(!authorized)throw new UnauthorizedAccessException("Active administrator required.");
        var rows=await db.Database.SqlQueryRaw<Row>(
            """
            SELECT o.id AS "OrderId",o.customer_user_id AS "CustomerUserId",
                o.plan_id AS "PlanId",o.gross_amount_irr AS "GrossIrr",
                o.collected_amount_irr AS "CollectedIrr",
                o.issued_at_utc AS "IssuedAtUtc",p.code AS "PlanCode"
            FROM commercial_orders o JOIN subscription_plan_catalog p ON p.id=o.plan_id
            WHERE o.status IN (1,2) AND o.gross_amount_irr>o.collected_amount_irr
              AND ((o.partner_user_id={0}) OR
                 (o.partner_user_id IS NULL AND o.root_admin_user_id={0}))
            ORDER BY o.issued_at_utc DESC LIMIT 100
            """,actor).ToArrayAsync(ct);
        return rows.Select(x=>new PendingRow(x.OrderId,x.CustomerUserId,x.PlanId,
            x.GrossIrr,x.CollectedIrr,x.IssuedAtUtc,x.PlanCode)).ToArray();
    }
    private sealed class Row
    {
        public Guid OrderId{get;set;}
        public Guid CustomerUserId{get;set;}
        public Guid PlanId{get;set;}
        public long GrossIrr{get;set;}
        public long CollectedIrr{get;set;}
        public DateTime IssuedAtUtc{get;set;}
        public string PlanCode{get;set;}="";
    }
}
