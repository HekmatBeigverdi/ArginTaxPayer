using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Internal credit accounting only. SMS delivery integration must call Spend
/// with its own stable send id after send confirmation; no speculative debit.</summary>
public sealed class SmsWalletService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public async Task<SmsWalletBalance> ReadAsync(string? bearer, Guid customerId,
        CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        if(!await IsInScopeAsync(actor,customerId,ct))
            throw new UnauthorizedAccessException("Customer not in scope.");
        var record=await db.Database.SqlQueryRaw<BalanceRow>(
            """
            SELECT granted_units AS "Granted",spent_units AS "Spent"
            FROM sms_wallet_accounts WHERE customer_user_id={0}
            """,customerId).SingleOrDefaultAsync(ct);
        return record is null?new SmsWalletBalance(0,0):new SmsWalletBalance(record.Granted,record.Spent);
    }

    public async Task<SmsWalletBalance> GrantAsync(string? bearer,Guid customerId,
        long units,string key,string evidence,CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var root=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct);
        if(!root || !await db.CustomerCommercialOwners.AsNoTracking()
             .AnyAsync(o=>o.CustomerUserId==customerId && o.RootLevel1UserId==actor,ct))
            throw new UnauthorizedAccessException("Level1 required.");
        return await ApplyAsync(customerId,units,key,evidence,1,actor,ct);
    }

    // Only a trusted SMS delivery coordinator may call this method; no HTTP route.
    internal Task<SmsWalletBalance> SpendConfirmedAsync(Guid customerId,long units,
        string stableSendKey,CancellationToken ct=default)
        => ApplyAsync(customerId,units,stableSendKey,"sms-confirmed",2,null,ct);

    private async Task<bool> IsInScopeAsync(Guid actor,Guid customerId,CancellationToken ct)
    {
        if(actor==customerId)return true;
        return await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(o=>
            o.CustomerUserId==customerId &&
            (o.RootLevel1UserId==actor || o.PartnerLevel2UserId==actor),ct);
    }

    private async Task<SmsWalletBalance> ApplyAsync(Guid customerId,long units,
        string key,string reference,int kind,Guid? approvedBy,CancellationToken ct)
    {
        if(customerId==Guid.Empty || units<=0 || string.IsNullOrWhiteSpace(key) ||
            key.Length>160 || string.IsNullOrWhiteSpace(reference) || reference.Length>160)
            throw new ArgumentException("Invalid SMS wallet operation.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var conn=db.Database.GetDbConnection();
        await using var create=conn.CreateCommand();
        create.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        create.CommandText="""
            INSERT INTO sms_wallet_accounts(customer_user_id,granted_units,spent_units)
            VALUES(@customer,0,0) ON CONFLICT(customer_user_id) DO NOTHING
            """;
        Add(create,"customer",customerId);
        await create.ExecuteNonQueryAsync(ct);
        await using var lockCommand=conn.CreateCommand();
        lockCommand.Transaction=create.Transaction;
        lockCommand.CommandText="""
            SELECT granted_units,spent_units FROM sms_wallet_accounts
            WHERE customer_user_id=@customer FOR UPDATE
            """;
        Add(lockCommand,"customer",customerId);
        long granted,spent;
        await using(var reader=await lockCommand.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("SMS wallet missing.");
            granted=reader.GetInt64(0);spent=reader.GetInt64(1);
        }
        await using var duplicate=conn.CreateCommand();
        duplicate.Transaction=create.Transaction;
        duplicate.CommandText="""
            SELECT kind,units,reference FROM sms_wallet_events
            WHERE customer_user_id=@customer AND operation_key=@key
            """;
        Add(duplicate,"customer",customerId);Add(duplicate,"key",key);
        await using(var reader=await duplicate.ExecuteReaderAsync(ct))
        {
            if(await reader.ReadAsync(ct))
            {
                if(reader.GetInt32(0)!=kind || reader.GetInt64(1)!=units ||
                   reader.GetString(2)!=reference)
                    throw new InvalidOperationException("SMS wallet idempotency conflict.");
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                return new SmsWalletBalance(granted,spent);
            }
        }
        var state=new SmsWalletBalance(granted,spent);
        var next=kind==1?state.Grant(units):state.Spend(units);
        await using var insert=conn.CreateCommand();
        insert.Transaction=create.Transaction;
        insert.CommandText="""
            INSERT INTO sms_wallet_events(id,customer_user_id,operation_key,kind,units,
                approved_by_level1_user_id,reference,recorded_at_utc)
            VALUES(@id,@customer,@key,@kind,@units,@approver,@reference,@now)
            """;
        Add(insert,"id",Guid.NewGuid());Add(insert,"customer",customerId);
        Add(insert,"key",key);Add(insert,"kind",kind);Add(insert,"units",units);
        Add(insert,"approver",approvedBy ?? (object)DBNull.Value);
        Add(insert,"reference",reference);Add(insert,"now",DateTime.UtcNow);
        await insert.ExecuteNonQueryAsync(ct);
        await using var update=conn.CreateCommand();
        update.Transaction=create.Transaction;
        update.CommandText="""
            UPDATE sms_wallet_accounts SET granted_units=@granted,spent_units=@spent,
                revision=revision+1 WHERE customer_user_id=@customer
            """;
        Add(update,"granted",next.Granted);Add(update,"spent",next.Spent);
        Add(update,"customer",customerId);
        if(await update.ExecuteNonQueryAsync(ct)!=1)
            throw new InvalidOperationException("SMS wallet balance update failed.");
        await tx.CommitAsync(ct);
        return next;
    }
    private sealed class BalanceRow
    {
        public long Granted {get;set;}
        public long Spent {get;set;}
    }
    private static void Add(DbCommand cmd,string key,object value)
    {
        var p=cmd.CreateParameter();p.ParameterName=key;p.Value=value;cmd.Parameters.Add(p);
    }
}
