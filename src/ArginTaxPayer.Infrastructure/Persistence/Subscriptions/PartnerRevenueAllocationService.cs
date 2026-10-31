using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Root-controlled cash allocation. One immutable allocation per verified receipt.
/// Cannot post an estimate based solely on an issued order.</summary>
public sealed class PartnerRevenueAllocationService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public async Task<Guid> AllocateAsync(string? accessToken, Guid collectionId,
        CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(accessToken,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var root=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct);
        if(!root) throw new UnauthorizedAccessException("Level1 and MFA required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var conn=db.Database.GetDbConnection();
        await using var cmd=conn.CreateCommand();
        cmd.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText="""
            SELECT c.amount_irr,o.root_admin_user_id,o.partner_user_id,c.verified_at_utc
            FROM commercial_collections c
            JOIN commercial_orders o ON o.id=c.order_id
            WHERE c.id=@id FOR UPDATE OF c
            """;
        Add(cmd,"id",collectionId);
        long amount; Guid rootId; Guid? partner; DateTime collectedAt;
        await using(var rd=await cmd.ExecuteReaderAsync(ct))
        {
            if(!await rd.ReadAsync(ct))throw new InvalidOperationException("Verified collection not found.");
            amount=rd.GetInt64(0);rootId=rd.GetGuid(1);
            partner=rd.IsDBNull(2)?null:rd.GetGuid(2);
            collectedAt=rd.GetDateTime(3);
        }
        if(rootId!=actor) throw new UnauthorizedAccessException("Root scope mismatch.");

        await using var previous=conn.CreateCommand();
        previous.Transaction=cmd.Transaction;
        previous.CommandText="SELECT id FROM commercial_revenue_allocations WHERE collection_id=@id";
        Add(previous,"id",collectionId);
        var existing=await previous.ExecuteScalarAsync(ct);
        if(existing is Guid prior)
        {
            await tx.CommitAsync(ct);
            return prior;
        }

        await using var costCmd=conn.CreateCommand();
        costCmd.Transaction=cmd.Transaction;
        costCmd.CommandText="""
            SELECT COALESCE(SUM(amount_irr),0) FROM commercial_approved_costs
            WHERE collection_id=@id AND approved_by_level1_user_id=@root
            """;
        Add(costCmd,"id",collectionId);Add(costCmd,"root",rootId);
        var cost=Convert.ToInt64(await costCmd.ExecuteScalarAsync(ct));
        Guid? termId=null;
        var share=new PartnerShareTerms(0);
        if(partner is not null)
        {
            await using var term=conn.CreateCommand();
            term.Transaction=cmd.Transaction;
            term.CommandText="""
                SELECT id,partner_basis_points FROM partner_share_terms
                WHERE root_user_id=@root AND partner_user_id=@partner
                  AND effective_from_utc<=@at
                  AND (effective_until_utc IS NULL OR effective_until_utc>@at)
                ORDER BY effective_from_utc DESC LIMIT 2
                """;
            Add(term,"root",rootId);Add(term,"partner",partner.Value);Add(term,"at",collectedAt);
            await using var rd=await term.ExecuteReaderAsync(ct);
            if(!await rd.ReadAsync(ct))throw new InvalidOperationException("Missing approved partner share terms.");
            termId=rd.GetGuid(0);share=new PartnerShareTerms(rd.GetInt32(1));
            if(await rd.ReadAsync(ct))throw new InvalidOperationException("Overlapping partner share terms.");
        }
        var allocation=PartnerRevenuePolicy.Calculate(amount,cost,share);
        var allocationId=Guid.NewGuid();
        await using var insert=conn.CreateCommand();
        insert.Transaction=cmd.Transaction;
        insert.CommandText="""
            INSERT INTO commercial_revenue_allocations
            (id,collection_id,root_user_id,partner_user_id,share_term_id,
             gross_collected_irr,approved_cost_irr,partner_share_irr,root_share_irr,posted_at_utc)
            VALUES(@id,@collection,@root,@partner,@term,@gross,@cost,@partnerShare,@rootShare,@at)
            """;
        Add(insert,"id",allocationId);Add(insert,"collection",collectionId);
        Add(insert,"root",rootId);Add(insert,"partner",partner ?? (object)DBNull.Value);
        Add(insert,"term",termId ?? (object)DBNull.Value);
        Add(insert,"gross",allocation.CollectedIrr);Add(insert,"cost",allocation.ApprovedCostIrr);
        Add(insert,"partnerShare",allocation.PartnerShareIrr);Add(insert,"rootShare",allocation.RootShareIrr);
        Add(insert,"at",DateTime.UtcNow);
        await insert.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return allocationId;
    }

    private static void Add(DbCommand cmd,string key,object value)
    {
        var p=cmd.CreateParameter();p.ParameterName=key;p.Value=value;cmd.Parameters.Add(p);
    }
}
