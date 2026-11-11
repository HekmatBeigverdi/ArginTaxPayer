using System.Data;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record FiscalMemoryChangeView(Guid Id, Guid SubscriptionId, Guid CurrentMemoryId,
    Guid ProposedMemoryId, Guid TaxpayerId, string Reason, BindingExceptionStatus Status,
    DateTime RequestedAtUtc, DateTime? ReviewedAtUtc, string? DecisionReason);
public sealed record FiscalMemoryChangeWorkspace(Guid SubscriptionId, int PlanKind,
    Guid BoundTaxpayerId, Guid BoundMemoryId, IReadOnlyList<FiscalMemoryChangeView> Requests);

/// <summary>
/// A time-based single-taxpayer plan keeps its verified Production binding until
/// the root Level 1 admin reviews a same-taxpayer, successfully tested replacement.
/// Sandbox metadata is intentionally outside this workflow.
/// </summary>
public sealed class SubscriptionExceptionReviewService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    private async Task<Guid?> ActorAsync(string? bearer, CancellationToken ct) =>
        await identity.CurrentUserIdAsync(bearer, ct);

    private async Task<bool> IsRootLevel1WithMfaAsync(Guid actor, CancellationToken ct) =>
        await db.IdentityUsers.AsNoTracking().AnyAsync(u => u.Id == actor && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct);

    private async Task<(StoredCustomerSubscription Plan, StoredAnnualBinding Binding)?> ActiveBindingAsync(
        Guid customerId, Guid taxpayerId, CancellationToken ct)
    {
        var now=DateTime.UtcNow;
        var plans=await db.CustomerSubscriptions.AsNoTracking().Where(s=>s.CustomerUserId==customerId &&
            s.State==SubscriptionLifecycle.Active && s.ActivatedAtUtc<=now && s.ExpiresAtUtc>now &&
            (s.Kind==SubscriptionPlanKind.AnnualSingleTaxpayer || s.Kind==SubscriptionPlanKind.MonthlySingleTaxpayer ||
             s.Kind==SubscriptionPlanKind.QuarterlySingleTaxpayer || s.Kind==SubscriptionPlanKind.SemiAnnualSingleTaxpayer))
            .OrderByDescending(s=>s.ActivatedAtUtc).ToArrayAsync(ct);
        foreach(var plan in plans){
            var binding=await db.AnnualSubscriptionBindings.AsNoTracking().SingleOrDefaultAsync(b=>
                b.SubscriptionId==plan.Id && b.CustomerUserId==customerId && b.TaxpayerId==taxpayerId,ct);
            if(binding is not null)return (plan,binding);
        }
        return null;
    }

    private async Task<DateTime?> SuccessfulProductionTestAsync(Guid taxpayerId, Guid memoryId,
        CancellationToken ct)
    {
        var binding=await new FiscalMemoryConnectionHistory(db).CaptureRegisteredAsync(taxpayerId,memoryId,ct);
        if(binding is null||binding.Environment!=(int)FiscalConnectionEnvironment.Production)return null;
        var history=await new FiscalMemoryConnectionHistory(db).ReadAsync(binding,ct);
        return history.LastSuccess?.Status=="Authenticated"?history.LastSuccess.TestedAtUtc:null;
    }

    private static FiscalMemoryChangeView View(StoredBindingException row) => new(row.Id,row.SubscriptionId,
        row.CurrentMemoryId??Guid.Empty,row.ProposedMemoryId,row.ProposedTaxpayerId,row.Reason,row.Status,
        row.RequestedAtUtc,row.ReviewedAtUtc,row.DecisionReason);

    public async Task<FiscalMemoryChangeWorkspace?> ReadMineAsync(string? bearer,Guid taxpayerId,
        CancellationToken ct=default)
    {
        var actor=await ActorAsync(bearer,ct);
        if(actor is null)return null;
        var owned=await db.TaxpayerProfiles.AsNoTracking().AnyAsync(t=>t.Id==taxpayerId&&t.CustomerUserId==actor,ct);
        if(!owned)return null;
        var active=await ActiveBindingAsync(actor.Value,taxpayerId,ct);
        if(active is null)return null;
        var requests=await db.SubscriptionBindingExceptions.AsNoTracking().Where(r=>
            r.SubscriptionId==active.Value.Plan.Id && r.CustomerUserId==actor && r.ProposedTaxpayerId==taxpayerId)
            .OrderByDescending(r=>r.RequestedAtUtc).Take(20).ToArrayAsync(ct);
        return new(active.Value.Plan.Id,(int)active.Value.Plan.Kind,taxpayerId,
            active.Value.Binding.FiscalMemoryId,requests.Select(View).ToArray());
    }

    public async Task<Guid> RequestAsync(string? accessToken, Guid subscriptionId,
        Guid proposedTaxpayerId, Guid proposedMemoryId, string reason,
        CancellationToken ct = default)
    {
        var actor=await ActorAsync(accessToken,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        if(subscriptionId==Guid.Empty||proposedTaxpayerId==Guid.Empty||proposedMemoryId==Guid.Empty||
            string.IsNullOrWhiteSpace(reason)||reason.Trim().Length<10||reason.Length>1000)
            throw new ArgumentException("Valid same-taxpayer memory and reason required.");
        var plan=await db.CustomerSubscriptions.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==subscriptionId,ct)
            ??throw new InvalidOperationException("Subscription not found.");
        SubscriptionBindingPolicy.RequireEligible(plan.Kind,plan.State,plan.ActivatedAtUtc,plan.ExpiresAtUtc,DateTime.UtcNow);
        if(!SubscriptionBindingPolicy.IsSingleTaxpayer(plan.Kind))throw new InvalidOperationException("Single-taxpayer plan required.");
        var customer=plan.CustomerUserId==actor;
        var partner=await db.CustomerCommercialOwners.AsNoTracking().AnyAsync(x=>
            x.CustomerUserId==plan.CustomerUserId&&x.PartnerLevel2UserId==actor&&x.RootLevel1UserId==plan.RootAdminUserId,ct);
        if(!customer&&!partner)throw new UnauthorizedAccessException("Customer scope denied.");
        var current=await db.AnnualSubscriptionBindings.AsNoTracking().SingleOrDefaultAsync(b=>
            b.SubscriptionId==subscriptionId&&b.CustomerUserId==plan.CustomerUserId,ct)
            ??throw new InvalidOperationException("Verified Production binding not found.");
        if(current.TaxpayerId!=proposedTaxpayerId||current.FiscalMemoryId==proposedMemoryId)
            throw new InvalidOperationException("Only another Production memory on the already bound taxpayer may be requested.");
        if(!await db.FiscalMemories.AsNoTracking().AnyAsync(m=>m.Id==current.FiscalMemoryId&&m.TaxpayerId==current.TaxpayerId&&
            m.AuthorityEnvironment==FiscalConnectionEnvironment.Production,ct))
            throw new InvalidOperationException("Sandbox and unclassified bindings are not eligible for the Production memory-change flow.");
        var candidate=await db.FiscalMemories.AsNoTracking().SingleOrDefaultAsync(m=>
            m.Id==proposedMemoryId&&m.TaxpayerId==proposedTaxpayerId&&
            m.AuthorityEnvironment==FiscalConnectionEnvironment.Production&&m.Status==FiscalMemoryStatus.Active,ct);
        if(candidate is null||await SuccessfulProductionTestAsync(proposedTaxpayerId,proposedMemoryId,ct) is null)
            throw new InvalidOperationException("Proposed Production memory must pass a recorded authenticated connection test.");
        if(await db.SubscriptionBindingExceptions.AnyAsync(r=>r.SubscriptionId==subscriptionId&&r.Status==BindingExceptionStatus.Pending,ct))
            throw new InvalidOperationException("A memory change request is already pending.");
        var request=new StoredBindingException{Id=Guid.NewGuid(),SubscriptionId=subscriptionId,
            CustomerUserId=plan.CustomerUserId,RequesterUserId=actor,ProposedTaxpayerId=current.TaxpayerId,
            CurrentMemoryId=current.FiscalMemoryId,ProposedMemoryId=proposedMemoryId,Reason=reason.Trim(),
            Status=BindingExceptionStatus.Pending,RequestedAtUtc=DateTime.UtcNow};
        db.SubscriptionBindingExceptions.Add(request);
        await db.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task<IReadOnlyList<FiscalMemoryChangeView>?> ListPendingAsync(string? bearer,
        CancellationToken ct=default)
    {
        var actor=await ActorAsync(bearer,ct);
        if(actor is null||!await IsRootLevel1WithMfaAsync(actor.Value,ct))return null;
        var rows=await db.SubscriptionBindingExceptions.AsNoTracking().Where(r=>r.Status==BindingExceptionStatus.Pending&&
            db.CustomerSubscriptions.Any(s=>s.Id==r.SubscriptionId&&s.RootAdminUserId==actor.Value))
            .OrderBy(r=>r.RequestedAtUtc).Take(100).ToArrayAsync(ct);
        return rows.Select(View).ToArray();
    }

    public async Task ReviewAsync(string? accessToken, Guid requestId, bool approve,
        string decisionReason, CancellationToken ct = default)
    {
        if(requestId==Guid.Empty||string.IsNullOrWhiteSpace(decisionReason)||decisionReason.Trim().Length<10||decisionReason.Length>1000)
            throw new ArgumentException("Review reason required.");
        var actor=await ActorAsync(accessToken,ct)
            ??throw new UnauthorizedAccessException("Authentication required.");
        if(!await IsRootLevel1WithMfaAsync(actor,ct))throw new UnauthorizedAccessException("Level 1 MFA required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var request=await db.SubscriptionBindingExceptions.SingleOrDefaultAsync(r=>r.Id==requestId,ct)
            ??throw new InvalidOperationException("Request not found.");
        var plan=await db.CustomerSubscriptions.SingleOrDefaultAsync(s=>s.Id==request.SubscriptionId&&
            s.CustomerUserId==request.CustomerUserId&&s.RootAdminUserId==actor,ct)
            ??throw new UnauthorizedAccessException("Root subscription scope denied.");
        SubscriptionBindingPolicy.RequireEligible(plan.Kind,plan.State,plan.ActivatedAtUtc,plan.ExpiresAtUtc,DateTime.UtcNow);
        if(!SubscriptionBindingPolicy.IsSingleTaxpayer(plan.Kind)||request.Status!=BindingExceptionStatus.Pending||
            request.CurrentMemoryId is null||request.ProposedTaxpayerId==Guid.Empty)
            throw new InvalidOperationException("Request is stale or invalid.");
        var binding=await db.AnnualSubscriptionBindings.SingleOrDefaultAsync(b=>b.SubscriptionId==plan.Id&&
            b.CustomerUserId==plan.CustomerUserId,ct)??throw new InvalidOperationException("Verified binding not found.");
        if(binding.TaxpayerId!=request.ProposedTaxpayerId||binding.FiscalMemoryId!=request.CurrentMemoryId.Value)
            throw new InvalidOperationException("The current taxpayer or memory changed after submission.");
        var successfulAt=await SuccessfulProductionTestAsync(request.ProposedTaxpayerId,request.ProposedMemoryId,ct);
        if(successfulAt is null)throw new InvalidOperationException("Proposed Production memory no longer has a successful connection test.");
        request.ReviewerUserId=actor;request.ReviewedAtUtc=DateTime.UtcNow;request.DecisionReason=decisionReason.Trim();
        request.Status=approve?BindingExceptionStatus.Approved:BindingExceptionStatus.Rejected;
        await db.SaveChangesAsync(ct);
        if(approve){
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('argin.binding_memory_change_request_id',{request.Id.ToString("D")},true)",ct);
            var oldMemory=await db.FiscalMemories.SingleOrDefaultAsync(m=>m.Id==binding.FiscalMemoryId&&
                m.TaxpayerId==binding.TaxpayerId,ct)??throw new InvalidOperationException("Current Production memory not found.");
            oldMemory.Status=FiscalMemoryStatus.Retired;oldMemory.Revision++;oldMemory.UpdatedAtUtc=DateTime.UtcNow;
            binding.FiscalMemoryId=request.ProposedMemoryId;
            binding.VerifiedConnectedAtUtc=successfulAt.Value;
            binding.EvidenceReference="level1-approved-memory-change:"+request.Id.ToString("N");
            await db.SaveChangesAsync(ct);
            request.Status=BindingExceptionStatus.Applied;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
