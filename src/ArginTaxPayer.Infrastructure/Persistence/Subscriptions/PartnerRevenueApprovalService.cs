using System.Data.Common;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Only the current unique Level1 may approve cost deductions and partner rates.
/// Changes are append-only and never rewrite booked allocations.</summary>
public sealed class PartnerRevenueApprovalService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    private async Task<Guid> RequireRootAsync(string? bearer,CancellationToken ct)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var allowed=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
            u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct);
        if(!allowed)throw new UnauthorizedAccessException("Level1 with MFA required.");
        return actor;
    }

    public async Task<Guid> ApproveCollectionCostAsync(string? bearer,Guid collectionId,
        long amountIrr,string explanation,CancellationToken ct=default)
    {
        var root=await RequireRootAsync(bearer,ct);
        if(collectionId==Guid.Empty || amountIrr<=0 ||
            string.IsNullOrWhiteSpace(explanation) || explanation.Length>1000)
            throw new ArgumentException("Invalid cost.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var conn=db.Database.GetDbConnection();
        await using var guard=conn.CreateCommand();
        guard.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        guard.CommandText="""
            SELECT c.amount_irr,o.root_admin_user_id
            FROM commercial_collections c JOIN commercial_orders o ON o.id=c.order_id
            WHERE c.id=@collection FOR UPDATE OF c
            """;
        Add(guard,"collection",collectionId);
        long collected;Guid owner;
        await using(var reader=await guard.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("Collection not found.");
            collected=reader.GetInt64(0);owner=reader.GetGuid(1);
        }
        if(owner!=root)throw new UnauthorizedAccessException("Outside root scope.");
        if(amountIrr>collected)throw new InvalidOperationException("Approved costs cannot exceed collection.");
        await using var pending=conn.CreateCommand();
        pending.Transaction=guard.Transaction;
        pending.CommandText="""
            SELECT EXISTS(SELECT 1 FROM commercial_revenue_allocations WHERE collection_id=@collection)
            """;
        Add(pending,"collection",collectionId);
        if((bool)(await pending.ExecuteScalarAsync(ct) ?? false))
            throw new InvalidOperationException("Allocation already posted; corrections require reversal.");
        var id=Guid.NewGuid();
        await using var insert=conn.CreateCommand();
        insert.Transaction=guard.Transaction;
        insert.CommandText="""
            INSERT INTO commercial_approved_costs
            (id,collection_id,amount_irr,explanation,approved_by_level1_user_id,approved_at_utc)
            VALUES(@id,@collection,@amount,@explanation,@root,@now)
            """;
        Add(insert,"id",id);Add(insert,"collection",collectionId);
        Add(insert,"amount",amountIrr);Add(insert,"explanation",explanation.Trim());
        Add(insert,"root",root);Add(insert,"now",DateTime.UtcNow);
        await insert.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<Guid> ApprovePartnerTermsAsync(string? bearer,Guid partnerId,
        int partnerBasisPoints,DateTime fromUtc,DateTime? untilUtc,CancellationToken ct=default)
    {
        var root=await RequireRootAsync(bearer,ct);
        new PartnerShareTerms(partnerBasisPoints).Validate();
        if(partnerId==Guid.Empty || partnerId==root || fromUtc.Kind!=DateTimeKind.Utc ||
            (untilUtc.HasValue && (untilUtc.Value.Kind!=DateTimeKind.Utc || untilUtc<=fromUtc)))
            throw new ArgumentException("Invalid share interval.");
        var eligible=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==partnerId &&
            u.IsActive && u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2),ct);
        if(!eligible)throw new InvalidOperationException("Partner must be active Level2.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        // Lock the partner identity row to serialize term intervals for this partner.
        var conn=db.Database.GetDbConnection();
        await using var locked=conn.CreateCommand();
        locked.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        locked.CommandText="SELECT id FROM identity_users WHERE id=@partner FOR UPDATE";
        Add(locked,"partner",partnerId);
        if(await locked.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("Partner not found.");
        await using var overlap=conn.CreateCommand();
        overlap.Transaction=locked.Transaction;
        overlap.CommandText="""
            SELECT EXISTS(SELECT 1 FROM partner_share_terms
             WHERE root_user_id=@root AND partner_user_id=@partner
             AND (effective_until_utc IS NULL OR effective_until_utc>@from)
             AND (@until IS NULL OR effective_from_utc<@until))
            """;
        Add(overlap,"root",root);Add(overlap,"partner",partnerId);
        Add(overlap,"from",fromUtc);Add(overlap,"until",untilUtc ?? (object)DBNull.Value);
        if((bool)(await overlap.ExecuteScalarAsync(ct)??false))
            throw new InvalidOperationException("Overlapping approved share terms.");
        var id=Guid.NewGuid();
        await using var insert=conn.CreateCommand();
        insert.Transaction=locked.Transaction;
        insert.CommandText="""
            INSERT INTO partner_share_terms
            (id,root_user_id,partner_user_id,partner_basis_points,effective_from_utc,
             effective_until_utc,approved_by_level1_user_id)
            VALUES(@id,@root,@partner,@rate,@from,@until,@root)
            """;
        Add(insert,"id",id);Add(insert,"root",root);Add(insert,"partner",partnerId);
        Add(insert,"rate",partnerBasisPoints);Add(insert,"from",fromUtc);
        Add(insert,"until",untilUtc ?? (object)DBNull.Value);
        await insert.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return id;
    }

    private static void Add(DbCommand cmd,string key,object value)
    {
        var p=cmd.CreateParameter();p.ParameterName=key;p.Value=value;cmd.Parameters.Add(p);
    }
}
