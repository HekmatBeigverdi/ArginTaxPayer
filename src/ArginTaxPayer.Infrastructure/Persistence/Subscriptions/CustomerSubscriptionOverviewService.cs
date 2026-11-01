using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Customer-only subscription status. Never authorize invoice dispatch
/// using this display model: dispatch uses an atomic server-side entitlement gate.</summary>
public sealed class CustomerSubscriptionOverviewService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public sealed record Overview(Guid? SubscriptionId,string PlanName,int? PlanKind,
        DateTime? ExpiresAtUtc,long? RemainingInvoices,int RemainingDays,
        bool HasActivePlan);
    public async Task<Overview> ReadMineAsync(string? bearer,CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        if(!await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive,ct))
            throw new UnauthorizedAccessException("Inactive account.");
        var now=DateTime.UtcNow;
        var row=await db.Database.SqlQueryRaw<Row>(
            """
            SELECT s.id AS "SubscriptionId",p.code AS "PlanName",
                s.kind AS "PlanKind",s.expires_at_utc AS "ExpiresAtUtc",
                CASE WHEN s.kind=2 THEN COALESCE(c.available_units,0)
                     ELSE NULL END AS "RemainingInvoices"
            FROM customer_subscriptions s
            JOIN subscription_plan_catalog p ON p.id=s.plan_id
            LEFT JOIN subscription_credit_accounts c ON c.subscription_id=s.id
            WHERE s.customer_user_id={0} AND s.state=2
              AND s.activated_at_utc<={1} AND s.expires_at_utc>{1}
            ORDER BY s.activated_at_utc DESC,s.id DESC LIMIT 1
            """,actor,now).SingleOrDefaultAsync(ct);
        if(row is null)return new Overview(null,"نسخه آزمایشی",null,null,null,0,false);
        var remaining=Math.Max(0,(int)Math.Ceiling((row.ExpiresAtUtc-now).TotalDays));
        return new Overview(row.SubscriptionId,row.PlanName,row.PlanKind,row.ExpiresAtUtc,
            row.RemainingInvoices,remaining,true);
    }
    private sealed class Row
    {
        public Guid SubscriptionId {get;set;}
        public string PlanName {get;set;}="";
        public int PlanKind {get;set;}
        public DateTime ExpiresAtUtc {get;set;}
        public long? RemainingInvoices {get;set;}
    }
}
