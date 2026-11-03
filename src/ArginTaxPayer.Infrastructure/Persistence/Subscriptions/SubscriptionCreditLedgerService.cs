using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using ArginTaxPayer.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public enum CreditAction { Reserve, Consume, Release }

public sealed record CreditBalance(long Granted, long Available, long Reserved, long Consumed);

/// <summary>Serialized, idempotent credit transitions for quantity plans only.
/// This is an internal accounting boundary, not an invoice-send gate.</summary>
public sealed class SubscriptionCreditLedgerService(ArginTaxPayerDbContext db)
{
    public async Task<CreditBalance> ApplyAsync(Guid customerId, Guid subscriptionId,
        string operationKey, string reservationKey, CreditAction action, long units,
        DateTime nowUtc, CancellationToken ct = default)
    {
        if (customerId == Guid.Empty || subscriptionId == Guid.Empty ||
            units <= 0 || nowUtc.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(operationKey) || operationKey.Length > 160 ||
            string.IsNullOrWhiteSpace(reservationKey) || reservationKey.Length > 160)
            throw new ArgumentException("Invalid credit operation.");
        if (!Enum.IsDefined(action)) throw new ArgumentException("Invalid action.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Lock the balance row so competing requests cannot over-reserve or double-consume.
        var conn = db.Database.GetDbConnection();
        await using var lockCommand = conn.CreateCommand();
        lockCommand.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        lockCommand.CommandText = """
            SELECT granted_units,available_units,reserved_units,consumed_units
            FROM subscription_credit_accounts
            WHERE subscription_id=@sub AND customer_user_id=@customer FOR UPDATE
            """;
        Add(lockCommand,"sub",subscriptionId);
        Add(lockCommand,"customer",customerId);
        CreditBalanceRow account;
        await using(var locked=await lockCommand.ExecuteReaderAsync(ct))
        {
            if(!await locked.ReadAsync(ct)) throw new InvalidOperationException("Credit account missing.");
            account=new CreditBalanceRow {
                Granted=locked.GetInt64(0),Available=locked.GetInt64(1),
                Reserved=locked.GetInt64(2),Consumed=locked.GetInt64(3)
            };
        }
        var sub = await db.CustomerSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == subscriptionId && x.CustomerUserId == customerId, ct)
            ?? throw new InvalidOperationException("Subscription not found.");
        SubscriptionBindingPolicy.RequireEligible(sub.Kind, sub.State, sub.ActivatedAtUtc, sub.ExpiresAtUtc, nowUtc);
        if (sub.Kind != SubscriptionPlanKind.Quantity) throw new InvalidOperationException("Not a quantity plan.");
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText = """
            SELECT operation_type, reservation_key, units FROM subscription_credit_entries
            WHERE subscription_id=@sub AND operation_key=@key
            """;
        Add(cmd,"sub",subscriptionId); Add(cmd,"key",operationKey);
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                var type=reader.GetString(0);
                var key=reader.GetString(1);
                var count=reader.GetInt64(2);
                if (type != action.ToString().ToLowerInvariant() || key != reservationKey || count != units)
                    throw new InvalidOperationException("Idempotency key reused with different payload.");
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                return new(account.Granted,account.Available,account.Reserved,account.Consumed);
            }
        }
        long available=account.Available, reserved=account.Reserved, consumed=account.Consumed;
        if(action == CreditAction.Reserve)
        {
            if(available<units) throw new InvalidOperationException("Insufficient credit.");
            available-=units;reserved+=units;
        }
        else
        {
            await using var prior = conn.CreateCommand();
            prior.Transaction=cmd.Transaction;
            prior.CommandText="""
                SELECT COALESCE(SUM(CASE operation_type WHEN 'reserve' THEN units
                    WHEN 'consume' THEN -units WHEN 'release' THEN -units ELSE 0 END),0)
                FROM subscription_credit_entries WHERE subscription_id=@sub AND reservation_key=@reservation
                """;
            Add(prior,"sub",subscriptionId);Add(prior,"reservation",reservationKey);
            var remaining=Convert.ToInt64(await prior.ExecuteScalarAsync(ct));
            if(remaining<units) throw new InvalidOperationException("Reservation exhausted.");
            reserved-=units;
            if(action==CreditAction.Consume) consumed+=units;
            else available+=units;
        }
        await using var update=conn.CreateCommand();
        update.Transaction=cmd.Transaction;
        update.CommandText="""
            UPDATE subscription_credit_accounts
            SET available_units=@available,reserved_units=@reserved,consumed_units=@consumed,revision=revision+1
            WHERE subscription_id=@sub AND customer_user_id=@customer
            """;
        Add(update,"available",available);Add(update,"reserved",reserved);Add(update,"consumed",consumed);
        Add(update,"sub",subscriptionId);Add(update,"customer",customerId);
        if(await update.ExecuteNonQueryAsync(ct)!=1) throw new InvalidOperationException("Account changed.");
        await using var append=conn.CreateCommand();
        append.Transaction=cmd.Transaction;
        append.CommandText="""
            INSERT INTO subscription_credit_entries(id,subscription_id,customer_user_id,
              operation_key,operation_type,reservation_key,units,recorded_at_utc)
            VALUES(@id,@sub,@customer,@key,@type,@reservation,@units,@now)
            """;
        Add(append,"id",Guid.NewGuid());Add(append,"sub",subscriptionId);Add(append,"customer",customerId);
        Add(append,"key",operationKey);Add(append,"type",action.ToString().ToLowerInvariant());
        Add(append,"reservation",reservationKey);Add(append,"units",units);Add(append,"now",nowUtc);
        await append.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return new(account.Granted,available,reserved,consumed);
    }

    /// <summary>Called by a trusted, confirmed purchase activation workflow only.
    /// The caller must verify payment and subscription ownership before invoking this method.</summary>
    public async Task<CreditBalance> InitializeAsync(Guid customerId, Guid subscriptionId,
        string grantKey, DateTime nowUtc, CancellationToken ct = default)
    {
        if(customerId==Guid.Empty || subscriptionId==Guid.Empty || nowUtc.Kind!=DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(grantKey) || grantKey.Length>160)
            throw new ArgumentException("Invalid grant initialization.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var sub=await db.CustomerSubscriptions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id==subscriptionId && x.CustomerUserId==customerId,ct)
            ?? throw new InvalidOperationException("Subscription not found.");
        SubscriptionBindingPolicy.RequireEligible(sub.Kind,sub.State,sub.ActivatedAtUtc,sub.ExpiresAtUtc,nowUtc);
        if(sub.Kind!=SubscriptionPlanKind.Quantity || sub.InvoiceUnitLimit is not >0)
            throw new InvalidOperationException("Quantity subscription with positive units required.");
        var conn=db.Database.GetDbConnection();
        await using var cmd=conn.CreateCommand();
        cmd.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText="""
            INSERT INTO subscription_credit_accounts
                (subscription_id,customer_user_id,granted_units,available_units,reserved_units,consumed_units)
            VALUES (@sub,@customer,@units,@units,0,0)
            ON CONFLICT(subscription_id) DO NOTHING
            """;
        Add(cmd,"sub",subscriptionId);Add(cmd,"customer",customerId);Add(cmd,"units",sub.InvoiceUnitLimit.Value);
        var inserted=await cmd.ExecuteNonQueryAsync(ct);
        if(inserted==1)
        {
            await using var entry=conn.CreateCommand();
            entry.Transaction=cmd.Transaction;
            entry.CommandText="""
                INSERT INTO subscription_credit_entries
                (id,subscription_id,customer_user_id,operation_key,operation_type,reservation_key,units,recorded_at_utc)
                VALUES(@id,@sub,@customer,@key,'grant',NULL,@units,@now)
                """;
            Add(entry,"id",Guid.NewGuid());Add(entry,"sub",subscriptionId);
            Add(entry,"customer",customerId);Add(entry,"key",grantKey);
            Add(entry,"units",sub.InvoiceUnitLimit.Value);Add(entry,"now",nowUtc);
            await entry.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        if(inserted==0)
            throw new InvalidOperationException("Credit account already initialized; never grant twice.");
        return new(sub.InvoiceUnitLimit.Value,sub.InvoiceUnitLimit.Value,0,0);
    }

    private static void Add(System.Data.Common.DbCommand command,string name,object value)
    {
        var parameter=command.CreateParameter();
        parameter.ParameterName=name;
        parameter.Value=value;
        command.Parameters.Add(parameter);
    }

    private sealed class CreditBalanceRow
    {
        public long Granted {get;set;}
        public long Available {get;set;}
        public long Reserved {get;set;}
        public long Consumed {get;set;}
    }
}
