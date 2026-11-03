using System.Data;
using System.Text.Json;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Creates an internal commercial receivable and immutable invoice snapshot.
/// Does not capture payments or activate subscriptions.</summary>
public sealed class CommercialOrderService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity,
    SubscriptionCatalogRepository catalog)
{
    public async Task<CommercialOrderSnapshot> IssueAsync(string? accessToken, Guid customerId,
        Guid planId, string idempotencyKey, DateTime atUtc, CancellationToken ct = default)
    {
        if (customerId == Guid.Empty || planId == Guid.Empty || atUtc.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 160)
            throw new ArgumentException("Invalid order request.");
        var actor = await identity.CurrentUserIdAsync(accessToken, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var isAdmin = await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor &&
            u.IsActive && (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) ||
                u.Roles.Any(r => r.Role == IdentityRoleNames.Level2)) &&
            db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null), ct);
        if (!isAdmin) throw new UnauthorizedAccessException("Authorized administrator required.");
        var ownership = await db.CustomerCommercialOwners.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CustomerUserId == customerId, ct)
            ?? throw new UnauthorizedAccessException("Customer commercial ownership missing.");
        if (actor != ownership.RootLevel1UserId && actor != ownership.PartnerLevel2UserId)
            throw new UnauthorizedAccessException("Customer outside administrator scope.");
        var partner = actor == ownership.PartnerLevel2UserId ? actor : (Guid?)null;
        // Return a previously issued order before consulting today's pricing.
        // Price windows may change after the first request.
        var existing = await db.Database.SqlQueryRaw<ExistingCommercialOrder>(
            """
            SELECT id AS "Id", plan_id AS "PlanId",
                   price_version_id AS "PriceVersionId",
                   root_admin_user_id AS "RootAdminUserId",
                   partner_user_id AS "PartnerUserId",
                   gross_amount_irr AS "GrossAmountIrr",
                   issued_at_utc AS "IssuedAtUtc"
            FROM commercial_orders
            WHERE customer_user_id={0} AND idempotency_key={1}
            """, customerId, idempotencyKey).SingleOrDefaultAsync(ct);
        if(existing is not null)
        {
            if(existing.PlanId!=planId || existing.RootAdminUserId!=ownership.RootLevel1UserId ||
                existing.PartnerUserId!=partner)
                throw new InvalidOperationException("Idempotency conflict.");
            return new CommercialOrderSnapshot(existing.Id,customerId,existing.PlanId,
                existing.PriceVersionId,existing.RootAdminUserId,existing.PartnerUserId,
                existing.GrossAmountIrr,existing.IssuedAtUtc);
        }
        var quote = await catalog.QuoteAsync(customerId, planId,
            ownership.RootLevel1UserId, partner, atUtc, ct);
        // A retry must not recalculate or change the original price.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var conn = db.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText = """
            INSERT INTO commercial_orders
              (id,customer_user_id,plan_id,price_version_id,root_admin_user_id,partner_user_id,
               idempotency_key,gross_amount_irr,collected_amount_irr,status,issued_at_utc)
            VALUES(@id,@customer,@plan,@price,@root,@partner,@key,@gross,0,1,@issued)
            ON CONFLICT(customer_user_id,idempotency_key) DO NOTHING
            """;
        var orderId=Guid.NewGuid();
        Add(cmd,"id",orderId);Add(cmd,"customer",customerId);Add(cmd,"plan",planId);
        Add(cmd,"price",quote.PriceVersionId);Add(cmd,"root",quote.RootAdminUserId);
        Add(cmd,"partner",quote.PartnerUserId ?? (object)DBNull.Value);Add(cmd,"key",idempotencyKey);
        Add(cmd,"gross",quote.PriceIrr);Add(cmd,"issued",atUtc);
        var inserted=await cmd.ExecuteNonQueryAsync(ct);
        if(inserted == 0)
        {
            await using var old = conn.CreateCommand();
            old.Transaction=cmd.Transaction;
            old.CommandText="""
                SELECT id,plan_id,price_version_id,root_admin_user_id,partner_user_id,
                       gross_amount_irr,issued_at_utc
                FROM commercial_orders WHERE customer_user_id=@customer AND idempotency_key=@key
                """;
            Add(old,"customer",customerId);Add(old,"key",idempotencyKey);
            await using var rd=await old.ExecuteReaderAsync(ct);
            if(!await rd.ReadAsync(ct)) throw new InvalidOperationException("Order race.");
            var result=new CommercialOrderSnapshot(rd.GetGuid(0),customerId,rd.GetGuid(1),
                rd.GetGuid(2),rd.GetGuid(3),rd.IsDBNull(4)?null:rd.GetGuid(4),
                rd.GetInt64(5),rd.GetDateTime(6));
            if(result.PlanId != planId || result.RootAdminUserId != quote.RootAdminUserId ||
               result.PartnerUserId != quote.PartnerUserId)
                throw new InvalidOperationException("Idempotency key reused with different order.");
            await rd.DisposeAsync();
            await tx.CommitAsync(ct);
            return result;
        }
        var snapshot=new CommercialOrderSnapshot(orderId,customerId,planId,quote.PriceVersionId,
            quote.RootAdminUserId,quote.PartnerUserId,quote.PriceIrr,atUtc);
        snapshot.Validate();
        await using var invoice=conn.CreateCommand();
        invoice.Transaction=cmd.Transaction;
        invoice.CommandText="""
            INSERT INTO commercial_invoice_snapshots
              (order_id,invoice_number,invoice_json,created_at_utc)
            VALUES(@id,@number,CAST(@payload AS jsonb),@issued)
            """;
        Add(invoice,"id",orderId);Add(invoice,"number","FIN-"+orderId.ToString("N").ToUpperInvariant());
        Add(invoice,"payload",JsonSerializer.Serialize(snapshot));Add(invoice,"issued",atUtc);
        await invoice.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return snapshot;
    }

    private sealed class ExistingCommercialOrder
    {
        public Guid Id {get;set;}
        public Guid PlanId {get;set;}
        public Guid PriceVersionId {get;set;}
        public Guid RootAdminUserId {get;set;}
        public Guid? PartnerUserId {get;set;}
        public long GrossAmountIrr {get;set;}
        public DateTime IssuedAtUtc {get;set;}
    }

    private static void Add(System.Data.Common.DbCommand cmd,string name,object value)
    {
        var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);
    }
}
