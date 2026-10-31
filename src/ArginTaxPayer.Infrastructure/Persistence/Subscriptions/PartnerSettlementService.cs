using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Posts immutable partner-settlement evidence; financial transfers must be
/// independently confirmed by the operator before calling PayAsync.</summary>
public sealed class PartnerSettlementService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public async Task<Guid> RecordPaymentAsync(string? bearer, Guid allocationId,
        long amountIrr, string bankReference, string explanation, CancellationToken ct=default)
        => await PostAsync(bearer,allocationId,amountIrr,bankReference,explanation,null,ct);

    public async Task<Guid> ReversePaymentAsync(string? bearer, Guid allocationId,
        Guid originalPaymentId,string reversalReference,string explanation,CancellationToken ct=default)
        => await PostAsync(bearer,allocationId,0,reversalReference,explanation,originalPaymentId,ct);

    private async Task<Guid> PostAsync(string? bearer,Guid allocationId,long requestedAmount,
        string reference,string explanation,Guid? originalId,CancellationToken ct)
    {
        if(allocationId==Guid.Empty || (originalId is null && requestedAmount<=0) ||
            (originalId == Guid.Empty) || string.IsNullOrWhiteSpace(reference) ||
            reference.Length>160 || string.IsNullOrWhiteSpace(explanation) || explanation.Length>1000)
            throw new ArgumentException("Invalid settlement evidence.");
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var permitted=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct);
        if(!permitted)throw new UnauthorizedAccessException("Level1 with MFA required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var conn=db.Database.GetDbConnection();
        await using var locked=conn.CreateCommand();
        locked.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        locked.CommandText="""
            SELECT root_user_id,partner_user_id,partner_share_irr
            FROM commercial_revenue_allocations WHERE id=@allocation FOR UPDATE
            """;
        Add(locked,"allocation",allocationId);
        Guid root,partner;long earned;
        await using(var reader=await locked.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("Allocation not found.");
            root=reader.GetGuid(0);
            if(reader.IsDBNull(1))throw new InvalidOperationException("Direct root revenue has no partner settlement.");
            partner=reader.GetGuid(1);earned=reader.GetInt64(2);
        }
        if(root!=actor)throw new UnauthorizedAccessException("Root scope mismatch.");

        // The unique reference is scoped to the one root. A repeat returns the first entry,
        // but conflicting parameters cannot silently mutate a settled amount.
        await using var duplicate=conn.CreateCommand();
        duplicate.Transaction=locked.Transaction;
        duplicate.CommandText="""
            SELECT id,allocation_id,kind,amount_irr,original_event_id
            FROM partner_settlement_events
            WHERE root_user_id=@root AND external_reference=@reference
            """;
        Add(duplicate,"root",root);Add(duplicate,"reference",reference);
        await using(var reader=await duplicate.ExecuteReaderAsync(ct))
        {
            if(await reader.ReadAsync(ct))
            {
                var id=reader.GetGuid(0);
                if(reader.GetGuid(1)!=allocationId ||
                   reader.GetInt32(2)!=(originalId.HasValue?2:1) ||
                   (originalId is null && reader.GetInt64(3)!=requestedAmount) ||
                   (originalId.HasValue && (reader.IsDBNull(4) || reader.GetGuid(4)!=originalId.Value)))
                    throw new InvalidOperationException("Settlement reference conflict.");
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                return id;
            }
        }
        await using var totals=conn.CreateCommand();
        totals.Transaction=locked.Transaction;
        totals.CommandText="""
            SELECT COALESCE(SUM(CASE kind WHEN 1 THEN amount_irr ELSE 0 END),0),
                   COALESCE(SUM(CASE kind WHEN 2 THEN amount_irr ELSE 0 END),0)
            FROM partner_settlement_events WHERE allocation_id=@allocation
            """;
        Add(totals,"allocation",allocationId);
        long paid,reversed;
        await using(var rd=await totals.ExecuteReaderAsync(ct))
        {
            await rd.ReadAsync(ct);paid=rd.GetInt64(0);reversed=rd.GetInt64(1);
        }
        var position=new PartnerSettlementPosition(earned,paid,reversed);
        position.Validate();
        long amount=requestedAmount;
        if(originalId is Guid original)
        {
            await using var prior=conn.CreateCommand();
            prior.Transaction=locked.Transaction;
            prior.CommandText="""
                SELECT amount_irr FROM partner_settlement_events
                WHERE id=@id AND allocation_id=@allocation AND kind=1
                """;
            Add(prior,"id",original);Add(prior,"allocation",allocationId);
            var value=await prior.ExecuteScalarAsync(ct);
            if(value is null)throw new InvalidOperationException("Original settlement not found.");
            amount=Convert.ToInt64(value);
            await using var already=conn.CreateCommand();
            already.Transaction=locked.Transaction;
            already.CommandText="SELECT EXISTS(SELECT 1 FROM partner_settlement_events WHERE original_event_id=@original)";
            Add(already,"original",original);
            if((bool)(await already.ExecuteScalarAsync(ct) ?? false))
                throw new InvalidOperationException("Original settlement already reversed.");
            position.Reverse(amount);
        }
        else position.Pay(amount);
        var newId=Guid.NewGuid();
        await using var insert=conn.CreateCommand();
        insert.Transaction=locked.Transaction;
        insert.CommandText="""
            INSERT INTO partner_settlement_events
            (id,allocation_id,root_user_id,partner_user_id,kind,amount_irr,
             external_reference,original_event_id,recorded_by_level1_user_id,reason,recorded_at_utc)
            VALUES(@id,@allocation,@root,@partner,@kind,@amount,
                   @reference,@original,@actor,@reason,@now)
            """;
        Add(insert,"id",newId);Add(insert,"allocation",allocationId);
        Add(insert,"root",root);Add(insert,"partner",partner);
        Add(insert,"kind",originalId.HasValue?2:1);Add(insert,"amount",amount);
        Add(insert,"reference",reference.Trim());
        Add(insert,"original",originalId ?? (object)DBNull.Value);
        Add(insert,"actor",actor);Add(insert,"reason",explanation.Trim());
        Add(insert,"now",DateTime.UtcNow);
        await insert.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return newId;
    }

    private static void Add(DbCommand cmd,string name,object value)
    {
        var parameter=cmd.CreateParameter();parameter.ParameterName=name;parameter.Value=value;
        cmd.Parameters.Add(parameter);
    }
}
