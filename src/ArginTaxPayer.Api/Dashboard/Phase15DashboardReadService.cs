using System.Globalization;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Api.Dashboard;

/// <summary>Phase15 authenticated read model. All query scopes are server-resolved; no authority calls or writes.</summary>
public sealed class Phase15DashboardReadService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope,
    IdentityAuthorizationService identity, CustomerSubscriptionOverviewService subscriptions)
{
    public sealed record Window(string Key, DateTime FromUtc, DateTime ToUtcExclusive, string TimeZone = "Asia/Tehran");
    public sealed record StatusCounts(int LogicalInvoiceCount, int Accepted, int AcceptedCancellation,
        int Rejected, int Processing, int AwaitingInquiry, int Unknown, int Prepared,
        int Attention, int Unclassified, double? SuccessRatePercent);
    public sealed record Bucket(string Date, int Issued, int AcceptedByIssueDate);
    public sealed record Alert(Guid InvoiceId, Guid TaxpayerId, string Status, DateTime IssuedAtUtc, string Link);
    public sealed record Overview(int SchemaVersion, string Role, DateTime AsOfUtc, Window Period,
        StatusCounts InvoiceStatus, int InvoiceDrafts, CustomerSubscriptionOverviewService.Overview? Subscription);
    public sealed record Trend(int SchemaVersion, Window Period, Bucket[] Buckets);
    public sealed record Finance(int SchemaVersion, string Role, Window Period, string Currency,
        long SalesGrossIrr, long SalesCollectedForCohortIrr, long SalesOutstandingIrr,
        long CollectionsInPeriodIrr, long PartnerEarnedShareIrr, long RootEarnedShareIrr,
        long PartnerSettlementNetIrr);
    public sealed record Attention(int SchemaVersion, Window Period, Alert[] Items);
    public sealed record Recent(int SchemaVersion, Window Period, Alert[] Items);

    private sealed class InvoiceRow
    {
        public Guid RootId { get; set; }
        public Guid TaxpayerId { get; set; }
        public DateTime IssuedAtUtc { get; set; }
        public int Subject { get; set; }
        public string AuthorityStatus { get; set; } = "";
        public int? DispatchState { get; set; }
    }
    private sealed class FinanceRow
    {
        public long GrossIrr { get; set; }
        public long CollectedIrr { get; set; }
        public long OutstandingIrr { get; set; }
    }
    private sealed class AmountRow { public long Amount { get; set; } }
    private sealed record Actor(Guid Id, string Role, bool Level1);

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
    private static DateTime ToUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Tehran);

    public static Window ResolveWindow(string? key, string? from, string? to, DateTime now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, Tehran));
        var chosen = key ?? "30d";
        DateOnly start, end = today;
        switch (chosen)
        {
            case "7d": start = today.AddDays(-6); break;
            case "30d": start = today.AddDays(-29); break;
            case "90d": start = today.AddDays(-89); break;
            case "year":
                var pc = new PersianCalendar();
                var t = today.ToDateTime(TimeOnly.MinValue);
                start = DateOnly.FromDateTime(pc.ToDateTime(pc.GetYear(t), 1, 1, 0, 0, 0, 0));
                break;
            case "custom":
                if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out start) ||
                    !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out end))
                    throw new ArgumentException("Invalid custom date range.");
                break;
            default: throw new ArgumentException("Unsupported dashboard period.");
        }
        if (chosen != "custom" && (from is not null || to is not null))
            throw new ArgumentException("Date filters require custom period.");
        if (start > end || end > today || end.DayNumber - start.DayNumber > 365)
            throw new ArgumentException("Dashboard date range is invalid.");
        return new Window(chosen, ToUtc(start), ToUtc(end.AddDays(1)));
    }

    private async Task<Actor> AuthorizeAsync(string? bearer, bool financial, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bearer)) throw new UnauthorizedAccessException("Not authenticated.");
        var principal = await scope.ResolveAsync(bearer, ct);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            throw new UnauthorizedAccessException("Inactive account or missing MFA.");
        var roles = await identity.GetCurrentRolesAsync(bearer, ct) ?? [];
        var role = roles.Contains(IdentityRoleNames.Level1) ? "Level1" :
                   roles.Contains(IdentityRoleNames.Level2) ? "Level2" :
                   roles.Contains(IdentityRoleNames.Customer) ? "Customer" : "";
        if (string.IsNullOrEmpty(role) || financial && role == "Customer")
            throw new UnauthorizedAccessException("Insufficient dashboard permissions.");
        return new Actor(principal.ActorId, role, role == "Level1");
    }

    private async Task ValidateTaxpayerAsync(Actor actor, Guid? taxpayerId, CancellationToken ct)
    {
        if (!taxpayerId.HasValue) return;
        if (taxpayerId == Guid.Empty || !await scope.ScopedQuery(actor.Id, actor.Level1)
                .AnyAsync(x => x.Id == taxpayerId.Value, ct))
            throw new KeyNotFoundException("Unavailable taxpayer.");
    }

    // Restrict BEFORE grouping by actor and currently authorized taxpayer membership.
    // A report SUCCESS is official evidence; an acknowledged dispatch receipt is not.
    private const string InvoiceSql = """
        WITH RECURSIVE scoped AS (
          SELECT d.id,d.customer_user_id,d.taxpayer_id,d.issue_date_utc,d.subject
          FROM invoice_documents d
          JOIN taxpayer_profiles tp ON tp.id=d.taxpayer_id
             AND tp.customer_user_id=d.customer_user_id
          JOIN identity_users u ON u.id=d.customer_user_id AND u.is_active=true
          WHERE ({0}::uuid IS NULL OR d.taxpayer_id={0})
            AND (d.customer_user_id={1}
              OR ({2} AND EXISTS (
                  SELECT 1 FROM customer_commercial_owners co
                  WHERE co.customer_user_id=d.customer_user_id
                    AND co.root_level1_user_id={1}))
              OR ({3} AND EXISTS (
                  SELECT 1 FROM customer_commercial_owners co
                  WHERE co.customer_user_id=d.customer_user_id
                    AND co.partner_level2_user_id={1})))
        ), roots AS (
          SELECT s.* FROM scoped s
          WHERE NOT EXISTS(SELECT 1 FROM sandbox_invoice_legal_relations r
               JOIN scoped predecessor ON predecessor.id=r.predecessor_invoice_id
               WHERE r.successor_invoice_id=s.id
                 AND r.taxpayer_id=s.taxpayer_id
                 AND r.customer_user_id=s.customer_user_id)
        ), chain AS (
          SELECT r.id AS root_id,r.id AS node_id,r.taxpayer_id,r.customer_user_id,
            r.issue_date_utc AS root_issued,0 AS depth,ARRAY[r.id] AS visited
          FROM roots r WHERE r.issue_date_utc>={4} AND r.issue_date_utc<{5}
          UNION ALL
          SELECT c.root_id,s.id,c.taxpayer_id,c.customer_user_id,
            c.root_issued,c.depth+1,c.visited||s.id
          FROM chain c JOIN sandbox_invoice_legal_relations rel
            ON rel.predecessor_invoice_id=c.node_id
             AND rel.taxpayer_id=c.taxpayer_id
             AND rel.customer_user_id=c.customer_user_id
          JOIN scoped s ON s.id=rel.successor_invoice_id
            AND s.taxpayer_id=c.taxpayer_id AND s.customer_user_id=c.customer_user_id
          WHERE c.depth<100 AND NOT s.id=ANY(c.visited)
        ), evaluations AS (
          SELECT c.root_id AS "RootId",c.taxpayer_id AS "TaxpayerId",
            c.root_issued AS "IssuedAtUtc",d.subject AS "Subject",
            COALESCE(report.status,'') AS "AuthorityStatus",
            op.state AS "DispatchState",c.depth,
            ROW_NUMBER() OVER (PARTITION BY c.root_id
              ORDER BY CASE WHEN report.status='SUCCESS' THEN 1 ELSE 0 END DESC,
                c.depth DESC,d.id DESC) AS rn
          FROM chain c JOIN scoped d ON d.id=c.node_id
          LEFT JOIN LATERAL (
            SELECT ar.report_json->>'Status' AS status
            FROM sandbox_authority_inquiry_reports ar
            WHERE ar.invoice_id=d.id AND ar.taxpayer_id=d.taxpayer_id
              AND ar.customer_user_id=d.customer_user_id
            ORDER BY ar.observed_at_utc DESC,ar.id DESC LIMIT 1
          ) report ON true
          LEFT JOIN LATERAL (
            SELECT state FROM invoice_dispatch_operations op
            WHERE op.invoice_id=d.id AND op.taxpayer_id=d.taxpayer_id
              AND op.customer_user_id=d.customer_user_id
            ORDER BY op.invoice_revision DESC,op.created_at_utc DESC,op.id DESC LIMIT 1
          ) op ON true
        )
        SELECT "RootId","TaxpayerId","IssuedAtUtc","Subject",
          "AuthorityStatus","DispatchState" FROM evaluations WHERE rn=1
        ORDER BY "IssuedAtUtc" DESC,"RootId" DESC
        """;

    private async Task<InvoiceRow[]> ReadInvoices(Actor actor, Window range, Guid? taxpayer, CancellationToken ct)
        => await db.Database.SqlQueryRaw<InvoiceRow>(InvoiceSql, taxpayer, actor.Id,
            actor.Level1, actor.Role == "Level2", range.FromUtc, range.ToUtcExclusive)
            .AsNoTracking().ToArrayAsync(ct);

    internal static string Classify(string? authority, int? dispatch)
    {
        if (string.Equals(authority, "SUCCESS", StringComparison.OrdinalIgnoreCase)) return "accepted";
        if (authority is not null && (authority.Equals("FAILED", StringComparison.OrdinalIgnoreCase)
              || authority.Equals("REJECTED", StringComparison.OrdinalIgnoreCase))) return "rejected";
        if (!string.IsNullOrWhiteSpace(authority) &&
            !authority.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)) return "unclassified";
        return dispatch switch
        {
            1 or 2 or 3 => "prepared",
            4 => "processing",
            5 => "awaitingInquiry",
            6 => "unknown",
            7 or 8 => "attention",
            null => "unclassified",
            _ => "unclassified"
        };
    }
    private static StatusCounts Count(InvoiceRow[] rows)
    {
        var groups = rows.GroupBy(x => Classify(x.AuthorityStatus, x.DispatchState))
            .ToDictionary(x => x.Key, x => x.Count());
        int Get(string name) => groups.GetValueOrDefault(name);
        var accepted = Get("accepted");
        var cancel = rows.Count(x => Classify(x.AuthorityStatus, x.DispatchState)=="accepted" && x.Subject==3);
        var rejected = Get("rejected");
        return new(rows.Length, accepted-cancel, cancel, rejected, Get("processing"),
            Get("awaitingInquiry"), Get("unknown"), Get("prepared"),
            Get("attention"), Get("unclassified"),
            accepted+rejected==0 ? null : Math.Round(100.0*accepted/(accepted+rejected),2));
    }
    public async Task<Overview> OverviewAsync(string? bearer, Window range, Guid? taxpayer, CancellationToken ct)
    {
        var actor=await AuthorizeAsync(bearer,false,ct);
        await ValidateTaxpayerAsync(actor,taxpayer,ct);
        var invoices=await ReadInvoices(actor,range,taxpayer,ct);
        var allowed=scope.ScopedQuery(actor.Id,actor.Level1).Select(x=>x.Id);
        var drafts=await db.InvoiceEntryWorkspaces.AsNoTracking()
            .Where(x=>allowed.Contains(x.TaxpayerId) &&
                (!taxpayer.HasValue || x.TaxpayerId==taxpayer.Value) &&
                x.UpdatedAtUtc>=range.FromUtc && x.UpdatedAtUtc<range.ToUtcExclusive)
            .CountAsync(ct);
        var subscription=actor.Role=="Customer" ? await subscriptions.ReadMineAsync(bearer,ct) : null;
        return new(1,actor.Role,DateTime.UtcNow,range,Count(invoices),drafts,subscription);
    }
    public async Task<Trend> TrendsAsync(string? bearer,Window range,Guid? taxpayer,CancellationToken ct)
    {
        var actor=await AuthorizeAsync(bearer,false,ct);
        await ValidateTaxpayerAsync(actor,taxpayer,ct);
        var invoices=await ReadInvoices(actor,range,taxpayer,ct);
        var buckets=invoices.GroupBy(x=>TimeZoneInfo.ConvertTimeFromUtc(x.IssuedAtUtc,Tehran)
                .ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))
            .OrderBy(x=>x.Key).Select(x=>new Bucket(x.Key,x.Count(),
                x.Count(y=>Classify(y.AuthorityStatus,y.DispatchState)=="accepted"))).ToArray();
        return new(1,range,buckets);
    }
    public async Task<Attention> AttentionAsync(string? bearer,Window range,Guid? taxpayer,CancellationToken ct)
    {
        var actor=await AuthorizeAsync(bearer,false,ct);
        await ValidateTaxpayerAsync(actor,taxpayer,ct);
        var rows=await ReadInvoices(actor,range,taxpayer,ct);
        return new(1,range,rows.Where(x=>new[]{"unknown","rejected","attention"}.Contains(
                Classify(x.AuthorityStatus,x.DispatchState)))
            .Take(10).Select(ToAlert).ToArray());
    }
    public async Task<Recent> RecentAsync(string? bearer,Window range,Guid? taxpayer,CancellationToken ct)
    {
        var actor=await AuthorizeAsync(bearer,false,ct);
        await ValidateTaxpayerAsync(actor,taxpayer,ct);
        var rows=await ReadInvoices(actor,range,taxpayer,ct);
        return new(1,range,rows.Take(10).Select(ToAlert).ToArray());
    }
    private static Alert ToAlert(InvoiceRow x)=>new(x.RootId,x.TaxpayerId,
        Classify(x.AuthorityStatus,x.DispatchState),x.IssuedAtUtc,"/invoices");

    public async Task<Finance> FinanceAsync(string? bearer,Window range,Guid? partnerUserId,CancellationToken ct)
    {
        var actor=await AuthorizeAsync(bearer,true,ct);
        if (partnerUserId.HasValue && (partnerUserId==Guid.Empty || !actor.Level1))
            throw new KeyNotFoundException("Unavailable partner.");
        if (partnerUserId.HasValue && !await db.CustomerCommercialOwners.AsNoTracking()
            .AnyAsync(x=>x.RootLevel1UserId==actor.Id && x.PartnerLevel2UserId==partnerUserId,ct))
            throw new KeyNotFoundException("Unavailable partner.");

        // Historical commercial transactions remain attributed to their actual order issuer.
        // 'collected for cohort' and 'collections in period' use different time dimensions.
        const string salesSql = """
          SELECT COALESCE(SUM(o.gross_amount_irr),0)::bigint AS "GrossIrr",
            COALESCE(SUM(o.collected_amount_irr),0)::bigint AS "CollectedIrr",
            COALESCE(SUM(o.gross_amount_irr-o.collected_amount_irr),0)::bigint AS "OutstandingIrr"
          FROM commercial_orders o WHERE o.status<>4
            AND o.issued_at_utc>={0} AND o.issued_at_utc<{1}
            AND (({2} AND o.root_admin_user_id={3}) OR ({4} AND o.partner_user_id={3}
              AND EXISTS (SELECT 1 FROM customer_commercial_owners own
                WHERE own.customer_user_id=o.customer_user_id AND own.partner_level2_user_id={3})))
            AND ({5}::uuid IS NULL OR o.partner_user_id={5})
          """;
        var sales=await db.Database.SqlQueryRaw<FinanceRow>(salesSql,
            range.FromUtc,range.ToUtcExclusive,actor.Level1,actor.Id,
            actor.Role=="Level2",partnerUserId).SingleAsync(ct);

        const string collectionsSql = """
          SELECT COALESCE(SUM(c.amount_irr),0)::bigint AS "Amount"
          FROM commercial_collections c JOIN commercial_orders o ON o.id=c.order_id
          WHERE c.verified_at_utc>={0} AND c.verified_at_utc<{1}
            AND o.status<>4 AND
            (({2} AND o.root_admin_user_id={3}) OR ({4} AND o.partner_user_id={3}))
            AND ({5}::uuid IS NULL OR o.partner_user_id={5})
          """;
        var collections=await db.Database.SqlQueryRaw<AmountRow>(collectionsSql,
            range.FromUtc,range.ToUtcExclusive,actor.Level1,actor.Id,
            actor.Role=="Level2",partnerUserId).SingleAsync(ct);
        const string sharesSql = """
          SELECT COALESCE(SUM(a.partner_share_irr),0)::bigint AS "GrossIrr",
            COALESCE(SUM(a.root_share_irr),0)::bigint AS "CollectedIrr",
            0::bigint AS "OutstandingIrr"
          FROM commercial_revenue_allocations a
          JOIN commercial_collections rc ON rc.id=a.collection_id
          JOIN commercial_orders ro ON ro.id=rc.order_id
          WHERE a.posted_at_utc>={0} AND a.posted_at_utc<{1}
            AND (({2} AND a.root_user_id={3}) OR ({4} AND a.partner_user_id={3}
              AND EXISTS (SELECT 1 FROM customer_commercial_owners own
                  WHERE own.customer_user_id=ro.customer_user_id AND own.partner_level2_user_id={3})))
            AND ({5}::uuid IS NULL OR a.partner_user_id={5})
          """;
        var shares=await db.Database.SqlQueryRaw<FinanceRow>(sharesSql,
            range.FromUtc,range.ToUtcExclusive,actor.Level1,actor.Id,
            actor.Role=="Level2",partnerUserId).SingleAsync(ct);
        const string settlementSql = """
          SELECT COALESCE(SUM(CASE WHEN e.kind=1 THEN e.amount_irr
                 WHEN e.kind=2 THEN -e.amount_irr ELSE 0 END),0)::bigint AS "Amount"
          FROM partner_settlement_events e JOIN commercial_revenue_allocations a
             ON a.id=e.allocation_id
          JOIN commercial_collections rc ON rc.id=a.collection_id
          JOIN commercial_orders ro ON ro.id=rc.order_id
          WHERE e.recorded_at_utc>={0} AND e.recorded_at_utc<{1}
            AND (({2} AND a.root_user_id={3}) OR ({4} AND a.partner_user_id={3}
              AND EXISTS (SELECT 1 FROM customer_commercial_owners own
                  WHERE own.customer_user_id=ro.customer_user_id AND own.partner_level2_user_id={3})))
            AND ({5}::uuid IS NULL OR a.partner_user_id={5})
          """;
        var settled=await db.Database.SqlQueryRaw<AmountRow>(settlementSql,
            range.FromUtc,range.ToUtcExclusive,actor.Level1,actor.Id,
            actor.Role=="Level2",partnerUserId).SingleAsync(ct);
        return new(1,actor.Role,range,"IRR",sales.GrossIrr,sales.CollectedIrr,
            sales.OutstandingIrr,collections.Amount,shares.GrossIrr,
            actor.Level1?shares.CollectedIrr:0,settled.Amount);
    }
}
