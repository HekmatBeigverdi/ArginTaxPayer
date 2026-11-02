using ArginTaxPayer.Domain.Subscriptions;
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
        string? CustomerDisplayName,string? CustomerContact,bool IsActivated,CollectionRow[] Collections,
        string? PartnerDisplayName);
    public sealed record CollectionRow(Guid CollectionId,Guid OrderId,long AmountIrr,int Channel,
        string ReceiptReference,DateTime ReceivedAtUtc);
    public sealed record SalesPage(PendingRow[] Items,long TotalGrossIrr,long TotalCollectedIrr,
        long TotalOutstandingIrr,int TotalCount,int Page,int PageSize)
    {
        public PartnerOption[] Partners{get;init;}=[];
    }
    public sealed class PartnerOption
    {
        public Guid PartnerUserId{get;set;}
        public string DisplayName{get;set;}="";
    }

    public Task<SalesPage> ReadMineAsync(string? bearer,int page=1,CancellationToken ct=default) =>
        ReadPageAsync(bearer,null,page,ct);

    public Task<SalesPage> ReadSalesAsync(string? bearer,string period,DateTime nowUtc,
        int page=1,CancellationToken ct=default,Guid? partnerUserId=null) =>
        ReadPageAsync(bearer,CommercialReportPeriod.Resolve(period,nowUtc),page,ct,partnerUserId);

    private const string ScopedSql = """

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
                (a.order_id IS NOT NULL) AS "IsActivated",
                pu.id AS "PartnerUserId",
                CASE WHEN pu.id IS NULL THEN NULL ELSE
                    COALESCE(NULLIF(trim(concat_ws(' ',NULLIF(pp.first_name,''),NULLIF(pp.last_name,''))),''),
                        NULLIF(trim(pp.company_name),''),pu.normalized_email,pu.normalized_mobile,
                        pu.id::text) END AS "PartnerDisplayName"
            FROM commercial_orders o JOIN subscription_plan_catalog p ON p.id=o.plan_id
            JOIN identity_users u ON u.id=o.customer_user_id
            LEFT JOIN software_user_profiles sp ON sp.user_id=u.id
            LEFT JOIN commercial_order_activations a ON a.order_id=o.id
            LEFT JOIN customer_commercial_owners co ON co.customer_user_id=o.customer_user_id
                AND (co.root_level1_user_id=o.root_admin_user_id
                    OR ({0} AND co.root_level1_user_id={2}))
            LEFT JOIN identity_users pu ON pu.id=CASE WHEN co.customer_user_id IS NOT NULL
                THEN co.partner_level2_user_id ELSE o.partner_user_id END
            LEFT JOIN software_user_profiles pp ON pp.user_id=pu.id
            WHERE o.status<>4
              AND (NOT {3} OR (o.issued_at_utc>={4} AND o.issued_at_utc<={5}))
              AND ({3} OR a.order_id IS NULL OR o.gross_amount_irr>o.collected_amount_irr)
              AND (({0} AND (o.root_admin_user_id={2} OR EXISTS (
                     SELECT 1 FROM customer_commercial_owners co
                     WHERE co.customer_user_id=o.customer_user_id AND co.root_level1_user_id={2})))
                   OR ({1} AND o.partner_user_id={2}))
            """;

    private async Task<(Guid Actor,bool Level1,bool Level2)> RequireAdminAsync(string? bearer,CancellationToken ct)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles=await db.IdentityUsers.AsNoTracking().Where(u=>u.Id==actor && u.IsActive)
            .Select(u=>new {Level1=u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1),
                Level2=u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2)})
            .SingleOrDefaultAsync(ct);
        if(roles is null || (!roles.Level1 && !roles.Level2))
            throw new UnauthorizedAccessException("Active administrator required.");
        return (actor,roles.Level1,roles.Level2);
    }

    private async Task<SalesPage> ReadPageAsync(string? bearer,CommercialReportPeriod? range,int page,
        CancellationToken ct,Guid? partnerUserId=null)
    {
        var offset=SubscriptionPaging.Offset(page);
        var actor=await RequireAdminAsync(bearer,ct);
        object[] parameters=[actor.Level1,actor.Level2,actor.Actor,range is not null,
            range?.FromUtc??DateTime.UnixEpoch,range?.ThroughUtc??DateTime.UnixEpoch,offset,
            partnerUserId.HasValue,partnerUserId??Guid.Empty];
        const string filter=" WHERE (NOT {7} OR scoped.\"PartnerUserId\"={8})";
        var totals=await db.Database.SqlQueryRaw<TotalsRow>(
            "SELECT COUNT(*)::int AS \"TotalCount\",COALESCE(SUM(\"GrossIrr\"),0)::bigint AS \"TotalGrossIrr\","
            + "COALESCE(SUM(\"CollectedIrr\"),0)::bigint AS \"TotalCollectedIrr\","
            + "COALESCE(SUM(GREATEST(0,\"GrossIrr\"-\"CollectedIrr\")),0)::bigint AS \"TotalOutstandingIrr\" FROM ("
            + ScopedSql + ") scoped"+filter,parameters).SingleAsync(ct);
        var rows=await db.Database.SqlQueryRaw<Row>("SELECT * FROM ("+ScopedSql+
            ") scoped"+filter+" ORDER BY \"IssuedAtUtc\" DESC,\"OrderId\" LIMIT 10 OFFSET {6}",parameters).ToArrayAsync(ct);
        PartnerOption[] partners=[];
        if(range is not null)
        {
            // Options cover the caller's permitted sales, independently of selected partner/period.
            var optionParameters=(object[])parameters.Clone();
            optionParameters[4]=DateTime.UnixEpoch;
            optionParameters[5]=range.ThroughUtc;
            partners=await db.Database.SqlQueryRaw<PartnerOption>(
                "SELECT DISTINCT \"PartnerUserId\",\"PartnerDisplayName\" AS \"DisplayName\" FROM ("+
                ScopedSql+") scoped WHERE \"PartnerUserId\" IS NOT NULL ORDER BY \"DisplayName\",\"PartnerUserId\"",
                optionParameters).ToArrayAsync(ct);
        }
        return new(rows.Select(x=>new PendingRow(x.OrderId,x.CustomerUserId,x.PlanId,
            x.GrossIrr,x.CollectedIrr,x.IssuedAtUtc,x.PlanCode,x.PlanKind,x.InvoiceUnitLimit,
            x.CustomerDisplayName,x.CustomerContact,x.IsActivated,[],x.PartnerDisplayName)).ToArray(),
            totals.TotalGrossIrr,totals.TotalCollectedIrr,totals.TotalOutstandingIrr,totals.TotalCount,page,10){Partners=partners};
    }

    public async Task<SubscriptionPage<CollectionRow>> ReadCollectionsAsync(string? bearer,Guid orderId,
        int page=1,CancellationToken ct=default)
    {
        var offset=SubscriptionPaging.Offset(page);
        var actor=await RequireAdminAsync(bearer,ct);
        // Authorize before reading receipt counts or references, including settled orders.
        var allowed=await db.Database.SqlQueryRaw<int>(
            """
            SELECT 1 AS "Value" FROM commercial_orders o WHERE o.id={0} AND
             (({1} AND (o.root_admin_user_id={3} OR EXISTS (
               SELECT 1 FROM customer_commercial_owners co WHERE co.customer_user_id=o.customer_user_id
                 AND co.root_level1_user_id={3}))) OR ({2} AND o.partner_user_id={3}))
            """,orderId,actor.Level1,actor.Level2,actor.Actor).AnyAsync(ct);
        if(!allowed)throw new UnauthorizedAccessException("Order outside administrator scope.");
        var total=await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*)::int AS \"Value\" FROM commercial_collections WHERE order_id={0}",orderId).SingleAsync(ct);
        var rows=await db.Database.SqlQueryRaw<CollectionHistoryDbRow>(
            """
            SELECT id AS "CollectionId",order_id AS "OrderId",amount_irr AS "AmountIrr",
                channel AS "Channel",external_reference AS "ReceiptReference",verified_at_utc AS "ReceivedAtUtc"
            FROM commercial_collections WHERE order_id={0}
            ORDER BY verified_at_utc DESC,id LIMIT 10 OFFSET {1}
            """,orderId,offset).ToArrayAsync(ct);
        return new(rows.Select(x=>new CollectionRow(x.CollectionId,x.OrderId,x.AmountIrr,x.Channel,
            x.ReceiptReference,x.ReceivedAtUtc)).ToArray(),total,page);
    }

    private sealed class TotalsRow
    {
        public int TotalCount{get;set;}
        public long TotalGrossIrr{get;set;}
        public long TotalCollectedIrr{get;set;}
        public long TotalOutstandingIrr{get;set;}
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
        public string? PartnerDisplayName{get;set;}
        public Guid? PartnerUserId{get;set;}
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
