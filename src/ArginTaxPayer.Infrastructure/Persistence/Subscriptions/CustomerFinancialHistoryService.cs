using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed class CustomerFinancialHistoryService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public sealed record PurchaseHistoryRow(Guid OrderId,Guid PlanId,string PlanCode,
        int PlanKind,long? InvoiceUnitLimit,long AmountIrr,long CollectedIrr,int Status,
        bool WasRejected,DateTime CreatedAtUtc,
        string InvoiceNumber,Guid? SubscriptionId,DateTime? ExpiresAtUtc);
    public async Task<PurchaseHistoryRow[]> ReadMineAsync(string? bearer,CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        if(!await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive,ct))
            throw new UnauthorizedAccessException("Inactive customer.");
        // No client-supplied customer id. Scope applies in SQL *before* returning rows.
        var rows=await db.Database.SqlQueryRaw<HistoryProjection>(
            """
            SELECT o.id AS "OrderId",o.plan_id AS "PlanId",
              p.code AS "PlanCode",p.kind AS "PlanKind",p.invoice_unit_limit AS "InvoiceUnitLimit",
              o.gross_amount_irr AS "AmountIrr",
              o.collected_amount_irr AS "CollectedIrr",
              o.status AS "Status",o.issued_at_utc AS "CreatedAtUtc",
              (r.order_id IS NOT NULL) AS "WasRejected",
              i.invoice_number AS "InvoiceNumber",
              a.subscription_id AS "SubscriptionId",
              ae.expires_at_utc AS "ExpiresAtUtc"
            FROM commercial_orders o
            JOIN subscription_plan_catalog p ON p.id=o.plan_id
            JOIN commercial_invoice_snapshots i ON i.order_id=o.id
            LEFT JOIN commercial_order_activations a ON a.order_id=o.id
            LEFT JOIN commercial_order_activation_expiries ae ON ae.order_id=o.id
            LEFT JOIN commercial_order_rejections r ON r.order_id=o.id
            WHERE o.customer_user_id={0}
            ORDER BY o.issued_at_utc DESC,o.id DESC LIMIT 100
            """,actor).ToArrayAsync(ct);
        return rows.Select(x=>new PurchaseHistoryRow(x.OrderId,x.PlanId,x.PlanCode,
            x.PlanKind,x.InvoiceUnitLimit,x.AmountIrr,x.CollectedIrr,x.Status,x.WasRejected,x.CreatedAtUtc,
            x.InvoiceNumber,x.SubscriptionId,x.ExpiresAtUtc)).ToArray();
    }
    private sealed class HistoryProjection
    {
        public Guid OrderId {get;set;}
        public Guid PlanId {get;set;}
        public string PlanCode {get;set;}="";
        public int PlanKind {get;set;}
        public long? InvoiceUnitLimit {get;set;}
        public long AmountIrr {get;set;}
        public long CollectedIrr {get;set;}
        public int Status {get;set;}
        public bool WasRejected {get;set;}
        public DateTime CreatedAtUtc {get;set;}
        public string InvoiceNumber {get;set;}="";
        public Guid? SubscriptionId {get;set;}
        public DateTime? ExpiresAtUtc {get;set;}
    }
}
