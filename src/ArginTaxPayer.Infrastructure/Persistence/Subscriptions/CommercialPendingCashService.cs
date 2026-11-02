using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Unapproved subscription requests and orders with outstanding balances.
/// Partners see attributed orders; root admins also resolve scope through current
/// customer ownership when an order's historical root snapshot differs.</summary>
public sealed class CommercialPendingCashService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public sealed record PendingRow(Guid OrderId,Guid CustomerUserId,Guid PlanId,
        long GrossIrr,long CollectedIrr,DateTime IssuedAtUtc,string PlanCode,
        int PlanKind,long? InvoiceUnitLimit,
        string? CustomerDisplayName,string? CustomerContact,bool IsActivated,CollectionRow[] Collections);
    public sealed record CollectionRow(Guid CollectionId,Guid OrderId,long AmountIrr,int Channel,
        string ReceiptReference,DateTime ReceivedAtUtc);
    public async Task<PendingRow[]> ReadMineAsync(string? bearer,CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles=await db.IdentityUsers.AsNoTracking().Where(u=>u.Id==actor && u.IsActive)
            .Select(u=>new {Level1=u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1),
                Level2=u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2)})
            .SingleOrDefaultAsync(ct);
        if(roles is null || (!roles.Level1 && !roles.Level2))
            throw new UnauthorizedAccessException("Active administrator required.");
        var rows=await db.Database.SqlQueryRaw<Row>(
            """
            SELECT o.id AS "OrderId",o.customer_user_id AS "CustomerUserId",
                o.plan_id AS "PlanId",o.gross_amount_irr AS "GrossIrr",
                o.collected_amount_irr AS "CollectedIrr",
                o.issued_at_utc AS "IssuedAtUtc",
                CASE WHEN char_length(p.code)>33 AND substring(p.code,33,1)='-'
                    AND substring(p.code,1,32) !~ '[^0-9a-f]'
                    THEN substring(p.code,34) ELSE p.code END AS "PlanCode",
                p.kind AS "PlanKind",p.invoice_unit_limit AS "InvoiceUnitLimit",
                COALESCE(NULLIF(trim(sp.company_name),''),
                    NULLIF(trim(concat_ws(' ',NULLIF(sp.first_name,''),NULLIF(sp.last_name,''))),''))
                    AS "CustomerDisplayName",
                COALESCE(u.normalized_email,u.normalized_mobile) AS "CustomerContact",
                (a.order_id IS NOT NULL) AS "IsActivated"
            FROM commercial_orders o JOIN subscription_plan_catalog p ON p.id=o.plan_id
            JOIN identity_users u ON u.id=o.customer_user_id
            LEFT JOIN software_user_profiles sp ON sp.user_id=u.id
            LEFT JOIN commercial_order_activations a ON a.order_id=o.id
            WHERE o.status<>4 AND (a.order_id IS NULL OR o.gross_amount_irr>o.collected_amount_irr)
              AND (({0} AND (o.root_admin_user_id={2} OR EXISTS (
                     SELECT 1 FROM customer_commercial_owners co
                     WHERE co.customer_user_id=o.customer_user_id AND co.root_level1_user_id={2})))
                   OR ({1} AND o.partner_user_id={2}))
            ORDER BY o.issued_at_utc DESC LIMIT 100
            """,roles.Level1,roles.Level2,actor).ToArrayAsync(ct);
        var orderIds=rows.Select(x=>x.OrderId).ToArray();
        var history=orderIds.Length==0 ? Array.Empty<CollectionHistoryDbRow>() :
            await db.Database.SqlQueryRaw<CollectionHistoryDbRow>(
            """
            SELECT id AS "CollectionId",order_id AS "OrderId",amount_irr AS "AmountIrr",
                channel AS "Channel",external_reference AS "ReceiptReference",
                verified_at_utc AS "ReceivedAtUtc"
            FROM commercial_collections WHERE order_id=ANY({0}::uuid[])
            ORDER BY verified_at_utc,id
            """,orderIds).ToArrayAsync(ct);
        var historyByOrder=history.GroupBy(x=>x.OrderId).ToDictionary(g=>g.Key,
            g=>g.Select(x=>new CollectionRow(x.CollectionId,x.OrderId,x.AmountIrr,x.Channel,
                x.ReceiptReference,x.ReceivedAtUtc)).ToArray());
        return rows.Select(x=>new PendingRow(x.OrderId,x.CustomerUserId,x.PlanId,
            x.GrossIrr,x.CollectedIrr,x.IssuedAtUtc,x.PlanCode,x.PlanKind,x.InvoiceUnitLimit,
            x.CustomerDisplayName,x.CustomerContact,x.IsActivated,
            historyByOrder.GetValueOrDefault(x.OrderId,Array.Empty<CollectionRow>()))).ToArray();
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
        public int PlanKind{get;set;}
        public long? InvoiceUnitLimit{get;set;}
        public string? CustomerDisplayName{get;set;}
        public string? CustomerContact{get;set;}
        public bool IsActivated{get;set;}
    }
    private sealed class CollectionHistoryDbRow
    {
        public Guid CollectionId{get;set;}
        public Guid OrderId{get;set;}
        public long AmountIrr{get;set;}
        public int Channel{get;set;}
        public string ReceiptReference{get;set;}="";
        public DateTime ReceivedAtUtc{get;set;}
    }
}
