using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Admin approval activates a commercial order regardless of its collection
/// balance. The outstanding amount remains on the order for later collection.</summary>
public sealed class CommercialSubscriptionActivationService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public Task<Guid> ApproveOrderAsync(string? bearer,Guid orderId,
        DateTime atUtc,CancellationToken ct=default)
    {
        if(orderId==Guid.Empty || atUtc.Kind!=DateTimeKind.Utc)
            throw new ArgumentException("Invalid order approval.");
        return db.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ApproveOrderCoreAsync(bearer,orderId,atUtc,ct));
    }

    private async Task<Guid> ApproveOrderCoreAsync(string? bearer,Guid orderId,
        DateTime atUtc,CancellationToken ct)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var allowed=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            (u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) ||
             u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2)) &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct);
        if(!allowed)throw new UnauthorizedAccessException("Authorized administrator MFA required.");
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
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("درخواست سفارش پیدا نشد.");
            customer=reader.GetGuid(0);plan=reader.GetGuid(1);price=reader.GetGuid(2);
            root=reader.GetGuid(3);partner=reader.IsDBNull(4)?null:reader.GetGuid(4);
            gross=reader.GetInt64(5);collected=reader.GetInt64(6);status=reader.GetInt32(7);
            purchased=reader.GetDateTime(8);kind=reader.GetInt32(9);validity=reader.GetInt32(10);
            limit=reader.IsDBNull(11)?null:reader.GetInt64(11);
        }
        await using(var customerLock=conn.CreateCommand())
        {
            customerLock.Transaction=lockOrder.Transaction;
            customerLock.CommandText="SELECT id FROM identity_users WHERE id=@customer FOR UPDATE";
            Add(customerLock,"customer",customer);
            await customerLock.ExecuteScalarAsync(ct);
        }
        if(actor!=root && actor!=partner)
        {
            var rootOwnsCustomer=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor &&
                u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) &&
                db.CustomerCommercialOwners.Any(o=>o.CustomerUserId==customer &&
                    o.RootLevel1UserId==actor),ct);
            if(!rootOwnsCustomer)
                throw new UnauthorizedAccessException("Order outside commercial owner scope.");
        }
        await using var existing=conn.CreateCommand();
        existing.Transaction=lockOrder.Transaction;
        existing.CommandText="SELECT subscription_id FROM commercial_order_activations WHERE order_id=@order";
        Add(existing,"order",orderId);
        if(await existing.ExecuteScalarAsync(ct) is Guid prior)
        {
            await tx.CommitAsync(ct);
            return prior;
        }
        if(gross<=0 || status==(int)CommercialOrderStatus.Voided)
            throw new InvalidOperationException("Only a valid, non-voided order can be approved.");
        var collections=await db.Database.SqlQueryRaw<CollectionProofRow>(
            """
            SELECT COALESCE(SUM(amount_irr),0) AS "TotalIrr"
            FROM commercial_collections WHERE order_id={0}
            """,orderId).SingleAsync(ct);
        if(collections.TotalIrr!=collected)
            throw new InvalidOperationException("Verified receipts do not match the recorded collection amount.");
        var selectedKind=(SubscriptionPlanKind)kind;
        if(selectedKind==SubscriptionPlanKind.Quantity && limit is not >0)
            throw new InvalidOperationException("Missing quantity entitlement.");
        Guid? activeSubscriptionId=null;
        var hasIncompatibleActive=false;
        await using(var active=conn.CreateCommand())
        {
            active.Transaction=lockOrder.Transaction;
            active.CommandText="""
                SELECT id,kind FROM customer_subscriptions
                WHERE customer_user_id=@customer AND state=2 AND activated_at_utc<=@now
                  AND expires_at_utc>@now
                ORDER BY activated_at_utc DESC,id DESC FOR UPDATE
                """;
            Add(active,"customer",customer);Add(active,"now",atUtc);
            await using var reader=await active.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))
            {
                var rowId=reader.GetGuid(0);
                var rowKind=(SubscriptionPlanKind)reader.GetInt32(1);
                activeSubscriptionId??=rowId;
                if(rowKind!=selectedKind)hasIncompatibleActive=true;
            }
        }
        if(activeSubscriptionId is not null && hasIncompatibleActive)
            throw new InvalidOperationException("درخواست پلن با نوع اشتراک فعال سازگار نیست؛ پس از پایان اشتراک فعلی دوباره تلاش کنید.");
        if(activeSubscriptionId is not null && selectedKind!=SubscriptionPlanKind.Quantity)
            throw new InvalidOperationException("تا پایان اشتراک زمانی فعال امکان فعال‌سازی درخواست زمانی جدید وجود ندارد.");
        int? duration=selectedKind==SubscriptionPlanKind.Quantity?12:null;
        var expires=SubscriptionCalendar.ExpirationUtc(selectedKind,atUtc,duration);
        var subscriptionId=activeSubscriptionId??Guid.NewGuid();
        if(activeSubscriptionId is null)
        {
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
        }
        else
        {
            await using var credit=conn.CreateCommand();
            credit.Transaction=lockOrder.Transaction;
            credit.CommandText="""
                UPDATE subscription_credit_accounts
                SET granted_units=granted_units+@units,available_units=available_units+@units,
                    revision=revision+1
                WHERE subscription_id=@subscription AND customer_user_id=@customer
                RETURNING granted_units
                """;
            Add(credit,"units",limit!.Value);Add(credit,"subscription",subscriptionId);
            Add(credit,"customer",customer);
            var totalUnits=await credit.ExecuteScalarAsync(ct) as long?
                ?? throw new InvalidOperationException("اشتراک تعدادی فعال، دفتر سهمیه معتبر ندارد.");
            await using var update=conn.CreateCommand();
            update.Transaction=lockOrder.Transaction;
            update.CommandText="""
                UPDATE customer_subscriptions SET plan_id=@plan,price_version_id=@price,
                    validity_days=@validity,invoice_unit_limit=@units,price_irr=price_irr+@gross,
                    root_admin_user_id=@root,partner_user_id=@partner,
                    purchased_at_utc=@purchased,activated_at_utc=@activated,
                    expires_at_utc=@expires,revision=revision+1
                WHERE id=@subscription AND customer_user_id=@customer
                """;
            Add(update,"plan",plan);Add(update,"price",price);Add(update,"validity",validity);
            Add(update,"units",totalUnits);Add(update,"gross",gross);Add(update,"root",root);
            Add(update,"partner",partner ?? (object)DBNull.Value);Add(update,"purchased",purchased);
            Add(update,"activated",atUtc);Add(update,"expires",expires);
            Add(update,"subscription",subscriptionId);Add(update,"customer",customer);
            if(await update.ExecuteNonQueryAsync(ct)!=1)
                throw new InvalidOperationException("اشتراک تعدادی هنگام افزودن سهمیه تغییر کرد.");
            await using var grant=conn.CreateCommand();
            grant.Transaction=lockOrder.Transaction;
            grant.CommandText="""
                INSERT INTO subscription_credit_entries
                  (id,subscription_id,customer_user_id,operation_key,operation_type,
                   reservation_key,units,recorded_at_utc)
                VALUES(@id,@subscription,@customer,@operation,'grant',NULL,@units,@at)
                """;
            Add(grant,"id",Guid.NewGuid());Add(grant,"subscription",subscriptionId);
            Add(grant,"customer",customer);Add(grant,"operation","activation-"+orderId.ToString("N"));
            Add(grant,"units",limit.Value);Add(grant,"at",atUtc);
            await grant.ExecuteNonQueryAsync(ct);
        }
        await using var link=conn.CreateCommand();
        link.Transaction=lockOrder.Transaction;
        link.CommandText="""
            INSERT INTO commercial_order_activations
            (order_id,subscription_id,approved_by_user_id,verified_at_utc)
            VALUES(@order,@subscription,@actor,@now)
            """;
        Add(link,"order",orderId);Add(link,"subscription",subscriptionId);
        Add(link,"actor",actor);Add(link,"now",atUtc);
        await link.ExecuteNonQueryAsync(ct);
        await using(var expiry=conn.CreateCommand())
        {
            expiry.Transaction=lockOrder.Transaction;
            expiry.CommandText="""
                INSERT INTO commercial_order_activation_expiries(order_id,expires_at_utc)
                VALUES(@order,@expires)
                """;
            Add(expiry,"order",orderId);Add(expiry,"expires",expires);
            await expiry.ExecuteNonQueryAsync(ct);
        }
        if(selectedKind==SubscriptionPlanKind.Quantity && activeSubscriptionId is null)
        {
            var units=limit.GetValueOrDefault();
            await using var credit=conn.CreateCommand();
            credit.Transaction=lockOrder.Transaction;
            credit.CommandText="""
                INSERT INTO subscription_credit_accounts
                (subscription_id,customer_user_id,granted_units,available_units,reserved_units,consumed_units)
                VALUES(@subscription,@customer,@units,@units,0,0)
                """;
            Add(credit,"subscription",subscriptionId);Add(credit,"customer",customer);
            Add(credit,"units",units);
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
            Add(journal,"units",units);Add(journal,"at",atUtc);
            await journal.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return subscriptionId;
    }

    public Task RejectOrderAsync(string? bearer,Guid orderId,DateTime atUtc,
        CancellationToken ct=default)
    {
        if(orderId==Guid.Empty || atUtc.Kind!=DateTimeKind.Utc)
            throw new ArgumentException("Invalid order rejection.");
        return db.Database.CreateExecutionStrategy().ExecuteAsync(
            () => RejectOrderCoreAsync(bearer,orderId,atUtc,ct));
    }

    private async Task RejectOrderCoreAsync(string? bearer,Guid orderId,DateTime atUtc,
        CancellationToken ct)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var allowed=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            (u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) ||
             u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2)) &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct);
        if(!allowed)throw new UnauthorizedAccessException("Authorized administrator MFA required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var conn=db.Database.GetDbConnection();
        await using var order=conn.CreateCommand();
        order.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        order.CommandText="""
            SELECT customer_user_id,root_admin_user_id,partner_user_id,
                   collected_amount_irr,status
            FROM commercial_orders WHERE id=@order FOR UPDATE
            """;
        Add(order,"order",orderId);
        Guid customer,root;Guid? partner;long collected;int status;
        await using(var reader=await order.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("درخواست سفارش پیدا نشد.");
            customer=reader.GetGuid(0);root=reader.GetGuid(1);
            partner=reader.IsDBNull(2)?null:reader.GetGuid(2);
            collected=reader.GetInt64(3);status=reader.GetInt32(4);
        }
        if(actor!=root && actor!=partner)
        {
            var rootOwnsCustomer=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor &&
                u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) &&
                db.CustomerCommercialOwners.Any(o=>o.CustomerUserId==customer &&
                    o.RootLevel1UserId==actor),ct);
            if(!rootOwnsCustomer)
                throw new UnauthorizedAccessException("Order outside commercial owner scope.");
        }
        await using var activation=conn.CreateCommand();
        activation.Transaction=order.Transaction;
        activation.CommandText="SELECT 1 FROM commercial_order_activations WHERE order_id=@order";
        Add(activation,"order",orderId);
        if(await activation.ExecuteScalarAsync(ct) is not null)
            throw new InvalidOperationException("اشتراک این سفارش فعال شده و درخواست قابل رد نیست.");
        if(status==(int)CommercialOrderStatus.Voided)
        {
            await tx.CommitAsync(ct);
            return;
        }
        if(collected!=0)
            throw new InvalidOperationException("سفارشی که برای آن وجه ثبت شده قابل رد نیست.");
        await using var cancel=conn.CreateCommand();
        cancel.Transaction=order.Transaction;
        cancel.CommandText="UPDATE commercial_orders SET status=@status,revision=revision+1 WHERE id=@order";
        Add(cancel,"status",(int)CommercialOrderStatus.Voided);Add(cancel,"order",orderId);
        if(await cancel.ExecuteNonQueryAsync(ct)!=1)
            throw new InvalidOperationException("لغو سفارش انجام نشد.");
        await using var audit=conn.CreateCommand();
        audit.Transaction=order.Transaction;
        audit.CommandText="""
            INSERT INTO commercial_order_rejections(order_id,rejected_by_user_id,rejected_at_utc)
            VALUES(@order,@actor,@at)
            """;
        Add(audit,"order",orderId);Add(audit,"actor",actor);Add(audit,"at",atUtc);
        await audit.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    private sealed class CollectionProofRow { public long TotalIrr {get;set;} }
    private static void Add(DbCommand cmd,string key,object value)
    {
        var p=cmd.CreateParameter();p.ParameterName=key;p.Value=value;cmd.Parameters.Add(p);
    }
}
