using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Records confirmed cash/bank receipts. Gateway receipts are internal-only
/// and must follow verification by the actual payment provider's server API.
/// Never call a gateway posting method directly from a callback payload.</summary>
public sealed class VerifiedCollectionService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<ReceivableBalance> RecordManualAsync(string? accessToken,
        VerifiedCollection collection, CancellationToken ct = default)
    {
        collection.Validate();
        if (collection.Channel == CollectionChannel.Gateway)
            throw new InvalidOperationException("Gateway confirmations require trusted provider verification.");
        var actor = await identity.CurrentUserIdAsync(accessToken, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var authorized = await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor &&
            u.IsActive &&
            (u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) ||
             u.Roles.Any(r => r.Role == IdentityRoleNames.Level2)) &&
            db.IdentityMfaStates.Any(m => m.UserId == actor && m.EnabledAtUtc != null),ct);
        if (!authorized) throw new UnauthorizedAccessException("Active admin MFA required.");
        var scope = await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(o =>
            o.CustomerUserId == collection.CustomerUserId &&
            (o.RootLevel1UserId == actor || o.PartnerLevel2UserId == actor),ct);
        if (!scope) throw new UnauthorizedAccessException("Outside commercial ownership.");
        return await PostVerifiedAsync(collection, actor, "admin-confirmed", ct);
    }

    /// <summary>Trust boundary: caller MUST first obtain independent server-to-server
    /// provider confirmation matching merchant, transaction id, currency and amount.
    /// No HTTP route exposes this method.</summary>
    internal Task<ReceivableBalance> PostGatewayVerifiedAsync(
        VerifiedCollection collection, string providerProof, CancellationToken ct = default)
    {
        collection.Validate();
        if (collection.Channel != CollectionChannel.Gateway || string.IsNullOrWhiteSpace(providerProof)
            || providerProof.Length > 160)
            throw new ArgumentException("Invalid gateway verification evidence.");
        return PostVerifiedAsync(collection, null, providerProof, ct);
    }

    private async Task<ReceivableBalance> PostVerifiedAsync(
        VerifiedCollection collection, Guid? verifiedBy, string proof, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var conn = db.Database.GetDbConnection();
        await using var locked = conn.CreateCommand();
        locked.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        locked.CommandText = """
            SELECT customer_user_id,gross_amount_irr,collected_amount_irr,status
            FROM commercial_orders WHERE id=@id FOR UPDATE
            """;
        Add(locked,"id",collection.OrderId);
        Guid orderOwner; long gross, collected; int status;
        await using (var rd=await locked.ExecuteReaderAsync(ct))
        {
            if (!await rd.ReadAsync(ct)) throw new InvalidOperationException("Order missing.");
            orderOwner=rd.GetGuid(0);gross=rd.GetInt64(1);
            collected=rd.GetInt64(2);status=rd.GetInt32(3);
        }
        if(orderOwner != collection.CustomerUserId || status == (int)CommercialOrderStatus.Voided)
            throw new InvalidOperationException("Order not collectible.");

        await using var duplicate = conn.CreateCommand();
        duplicate.Transaction=locked.Transaction;
        duplicate.CommandText = """
            SELECT order_id,customer_user_id,amount_irr,verifier_reference
            FROM commercial_collections WHERE channel=@channel AND external_reference=@reference
            """;
        Add(duplicate,"channel",(int)collection.Channel);
        Add(duplicate,"reference",collection.ExternalReference);
        await using(var rd=await duplicate.ExecuteReaderAsync(ct))
        {
            if(await rd.ReadAsync(ct))
            {
                if(rd.GetGuid(0)!=collection.OrderId || rd.GetGuid(1)!=collection.CustomerUserId
                   || rd.GetInt64(2)!=collection.AmountIrr || rd.GetString(3)!=proof)
                    throw new InvalidOperationException("Payment reference reused with different details.");
                await rd.DisposeAsync();
                await tx.CommitAsync(ct);
                return new ReceivableBalance(gross,collected);
            }
        }
        var current = new ReceivableBalance(gross,collected);
        var next = current.Collect(collection.AmountIrr);
        await using var post = conn.CreateCommand();
        post.Transaction=locked.Transaction;
        post.CommandText = """
            INSERT INTO commercial_collections
              (id,order_id,customer_user_id,channel,amount_irr,external_reference,
               verified_at_utc,verified_by_user_id,verifier_reference)
            VALUES(@id,@order,@owner,@channel,@amount,@reference,@at,@actor,@proof)
            """;
        Add(post,"id",Guid.NewGuid());Add(post,"order",collection.OrderId);
        Add(post,"owner",collection.CustomerUserId);Add(post,"channel",(int)collection.Channel);
        Add(post,"amount",collection.AmountIrr);Add(post,"reference",collection.ExternalReference);
        Add(post,"at",collection.VerifiedAtUtc);Add(post,"actor",verifiedBy ?? (object)DBNull.Value);
        Add(post,"proof",proof);
        await post.ExecuteNonQueryAsync(ct);
        await using var update=conn.CreateCommand();
        update.Transaction=locked.Transaction;
        update.CommandText="""
            UPDATE commercial_orders SET collected_amount_irr=@collected,status=@status,revision=revision+1
            WHERE id=@id AND customer_user_id=@owner
            """;
        Add(update,"collected",next.CollectedIrr);Add(update,"status",(int)next.Status);
        Add(update,"id",collection.OrderId);Add(update,"owner",collection.CustomerUserId);
        if(await update.ExecuteNonQueryAsync(ct)!=1)
            throw new InvalidOperationException("Order update failed.");
        await tx.CommitAsync(ct);
        return next;
    }

    private static void Add(DbCommand cmd,string name,object value)
    {
        var parameter=cmd.CreateParameter();
        parameter.ParameterName=name;parameter.Value=value;cmd.Parameters.Add(parameter);
    }
}
