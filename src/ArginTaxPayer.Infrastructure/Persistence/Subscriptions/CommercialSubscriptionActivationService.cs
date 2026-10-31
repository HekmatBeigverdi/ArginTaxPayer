using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Root-authorized activation after confirmed fully paid order.
/// A commercial order activates once, preserving the original price snapshot.
/// Renewal is a new paid order with a new immutable subscription record.</summary>
public sealed class CommercialSubscriptionActivationService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public async Task<Guid> ActivateFullyPaidOrderAsync(string? bearer,Guid orderId,
        DateTime atUtc,CancellationToken ct=default)
    {
        if(orderId==Guid.Empty || atUtc.Kind!=DateTimeKind.Utc)
            throw new ArgumentException("Invalid order activation.");
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var allowed=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct);
        if(!allowed)throw new UnauthorizedAccessException("Level1 MFA required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var conn=db.Database.GetDbConnection();
        await using var lockOrder=conn.CreateCommand();
        lockOrder.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        lockOrder.CommandText="""
            SELECT o.customer_user_id,o.plan_id,o.price_version_id,
              o.root_admin_user_id,o.partner_user_id,o.gross_amount_irr,
              o.collected_amount_irr,o.status,o.issued_at_utc,
              p.kind,p.validity_days,p.invoice_unit_limit
            FROM commercial_orders o
            JOIN subscription_plan_catalog p ON p.id=o.plan_id
            WHERE o.id=@order FOR UPDATE OF o
            """;
        Add(lockOrder,"order",orderId);
        Guid customer,plan,price,root;Guid? partner;
        long gross,collected;int status,kind,validity;long? limit;DateTime purchased;
        await using(var reader=await lockOrder.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("Order missing.");
            customer=reader.GetGuid(0);plan=reader.GetGuid(1);price=reader.GetGuid(2);
            root=reader.GetGuid(3);partner=reader.IsDBNull(4)?null:reader.GetGuid(4);
            gross=reader.GetInt64(5);collected=reader.GetInt64(6);status=reader.GetInt32(7);
            purchased=reader.GetDateTime(8);kind=reader.GetInt32(9);validity=reader.GetInt32(10);
            limit=reader.IsDBNull(11)?null:reader.GetInt64(11);
        }
        if(root!=actor)throw new UnauthorizedAccessException("Order outside root scope.");
        await using var existing=conn.CreateCommand();
        existing.Transaction=lockOrder.Transaction;
        existing.CommandText="SELECT subscription_id FROM commercial_order_activations WHERE order_id=@order";
        Add(existing,"order",orderId);
        if(await existing.ExecuteScalarAsync(ct) is Guid prior)
        {
            await tx.CommitAsync(ct);
            return prior;
        }
        if(gross<=0 || gross!=collected || status!=(int)CommercialOrderStatus.Paid)
            throw new InvalidOperationException("Order must have positive confirmed full payment.");
        var collections=await db.Database.SqlQueryRaw<CollectionProofRow>(
            """
            SELECT COALESCE(SUM(amount_irr),0) AS "TotalIrr"
            FROM commercial_collections WHERE order_id={0}
            """,orderId).SingleAsync(ct);
        if(collections.TotalIrr!=gross)
            throw new InvalidOperationException("Verified receipts do not match amount paid.");
        var selectedKind=(SubscriptionPlanKind)kind;
        int? duration=selectedKind==SubscriptionPlanKind.Quantity?12:null;
        var expires=SubscriptionCalendar.ExpirationUtc(selectedKind,atUtc,duration);
        var subscriptionId=Guid.NewGuid();
        await using var insert=conn.CreateCommand();
        insert.Transaction=lockOrder.Transaction;
        insert.CommandText="""
            INSERT INTO customer_subscriptions
            (id,customer_user_id,plan_id,price_version_id,kind,validity_days,
             invoice_unit_limit,price_irr,root_admin_user_id,partner_user_id,
             state,purchased_at_utc,activated_at_utc,expires_at_utc,revision)
            VALUES(@id,@customer,@plan,@price,@kind,@validity,@limit,@gross,
                   @root,@partner,2,@purchased,@activated,@expires,1)
            """;
        Add(insert,"id",subscriptionId);Add(insert,"customer",customer);Add(insert,"plan",plan);
        Add(insert,"price",price);Add(insert,"kind",kind);Add(insert,"validity",validity);
        Add(insert,"limit",limit ?? (object)DBNull.Value);Add(insert,"gross",gross);
        Add(insert,"root",root);Add(insert,"partner",partner ?? (object)DBNull.Value);
        Add(insert,"purchased",purchased);Add(insert,"activated",atUtc);Add(insert,"expires",expires);
        await insert.ExecuteNonQueryAsync(ct);
        await using var link=conn.CreateCommand();
        link.Transaction=lockOrder.Transaction;
        link.CommandText="""
            INSERT INTO commercial_order_activations
            (order_id,subscription_id,activated_by_level1_user_id,verified_at_utc)
            VALUES(@order,@subscription,@actor,@now)
            """;
        Add(link,"order",orderId);Add(link,"subscription",subscriptionId);
        Add(link,"actor",actor);Add(link,"now",atUtc);
        await link.ExecuteNonQueryAsync(ct);
        if(selectedKind==SubscriptionPlanKind.Quantity)
        {
            if(limit is not >0)throw new InvalidOperationException("Missing quantity entitlement.");
            await using var credit=conn.CreateCommand();
            credit.Transaction=lockOrder.Transaction;
            credit.CommandText="""
                INSERT INTO subscription_credit_accounts
                (subscription_id,customer_user_id,granted_units,available_units,reserved_units,consumed_units)
                VALUES(@subscription,@customer,@units,@units,0,0)
                """;
            Add(credit,"subscription",subscriptionId);Add(credit,"customer",customer);
            Add(credit,"units",limit.Value);
            await credit.ExecuteNonQueryAsync(ct);
            await using var journal=conn.CreateCommand();
            journal.Transaction=lockOrder.Transaction;
            journal.CommandText="""
                INSERT INTO subscription_credit_entries
                (id,subscription_id,customer_user_id,operation_key,operation_type,
                 reservation_key,units,recorded_at_utc)
                VALUES(@id,@subscription,@customer,@operation,'grant',NULL,@units,@at)
                """;
            Add(journal,"id",Guid.NewGuid());Add(journal,"subscription",subscriptionId);
            Add(journal,"customer",customer);Add(journal,"operation","activation-"+orderId.ToString("N"));
            Add(journal,"units",limit.Value);Add(journal,"at",atUtc);
            await journal.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return subscriptionId;
    }

    private sealed class CollectionProofRow { public long TotalIrr {get;set;} }
    private static void Add(DbCommand cmd,string key,object value)
    {
        var p=cmd.CreateParameter();p.ParameterName=key;p.Value=value;cmd.Parameters.Add(p);
    }
}
