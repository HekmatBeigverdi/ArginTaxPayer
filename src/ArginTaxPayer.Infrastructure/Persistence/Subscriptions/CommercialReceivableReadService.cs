using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record CommercialReceivableView(Guid OrderId, Guid CustomerUserId, Guid PlanId,
    Guid PriceVersionId, Guid RootAdminUserId, Guid? PartnerUserId,
    long GrossAmountIrr, long CollectedAmountIrr, long OutstandingAmountIrr,
    CommercialOrderStatus Status, DateTime IssuedAtUtc, string InvoiceNumber);

public sealed class CommercialReceivableReadService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<CommercialReceivableView> ReadAsync(
        string? accessToken, Guid customerId, Guid orderId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(accessToken, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var isAdmin = await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor &&
            u.IsActive && (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) ||
                u.Roles.Any(r => r.Role == IdentityRoleNames.Level2)), ct);
        if(!isAdmin) throw new UnauthorizedAccessException("Administrator required.");
        var owner = await db.CustomerCommercialOwners.AsNoTracking()
            .SingleOrDefaultAsync(o => o.CustomerUserId == customerId, ct)
            ?? throw new UnauthorizedAccessException("Customer ownership not found.");
        if(actor!=owner.RootLevel1UserId && actor!=owner.PartnerLevel2UserId)
            throw new UnauthorizedAccessException("Customer outside administrator scope.");
        var result=await db.Database.SqlQueryRaw<CommercialReceivableRow>(
            """
            SELECT o.id AS "OrderId",o.customer_user_id AS "CustomerUserId",
                o.plan_id AS "PlanId",o.price_version_id AS "PriceVersionId",
                o.root_admin_user_id AS "RootAdminUserId",o.partner_user_id AS "PartnerUserId",
                o.gross_amount_irr AS "GrossAmountIrr",
                o.collected_amount_irr AS "CollectedAmountIrr",o.status AS "Status",
                o.issued_at_utc AS "IssuedAtUtc",i.invoice_number AS "InvoiceNumber"
            FROM commercial_orders o
            JOIN commercial_invoice_snapshots i ON i.order_id=o.id
            WHERE o.customer_user_id={0} AND o.id={1}
            """,customerId,orderId).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Commercial order not found.");
        if(result.RootAdminUserId!=owner.RootLevel1UserId)
            throw new UnauthorizedAccessException("Order outside current commercial root.");
        if(actor!=result.RootAdminUserId && actor!=result.PartnerUserId)
            throw new UnauthorizedAccessException("Order outside administrator scope.");
        var balance=new ReceivableBalance(result.GrossAmountIrr,result.CollectedAmountIrr);
        balance.Validate();
        return new CommercialReceivableView(result.OrderId,customerId,result.PlanId,result.PriceVersionId,
            result.RootAdminUserId,result.PartnerUserId,balance.GrossIrr,balance.CollectedIrr,
            balance.OutstandingIrr,(CommercialOrderStatus)result.Status,result.IssuedAtUtc,
            result.InvoiceNumber);
    }

    private sealed class CommercialReceivableRow
    {
        public Guid OrderId {get;set;}
        public Guid CustomerUserId {get;set;}
        public Guid PlanId {get;set;}
        public Guid PriceVersionId {get;set;}
        public Guid RootAdminUserId {get;set;}
        public Guid? PartnerUserId {get;set;}
        public long GrossAmountIrr {get;set;}
        public long CollectedAmountIrr {get;set;}
        public int Status {get;set;}
        public DateTime IssuedAtUtc {get;set;}
        public string InvoiceNumber {get;set;}="";
    }
}
