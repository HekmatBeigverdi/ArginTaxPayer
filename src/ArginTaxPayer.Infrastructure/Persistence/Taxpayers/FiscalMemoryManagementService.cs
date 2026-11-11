using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public sealed record RegisterFiscalMemoryRequest(string? MemoryIdentifier,
    FiscalConnectionEnvironment? AuthorityEnvironment = null);
public sealed record ChangeFiscalMemoryStatusRequest(FiscalMemoryStatus Status, long ExpectedRevision);
public sealed record FiscalMemoryView(Guid Id, Guid TaxpayerId, string MemoryIdentifier,
    FiscalMemoryStatus Status, long Revision,
    FiscalConnectionEnvironment? AuthorityEnvironment = null,
    bool? LastConnectionTestSucceeded = null, DateTime? LastConnectionTestAtUtc = null,
    bool CanVerifyAndActivate = false);
public enum FiscalMemoryOperationStatus
{
    Success, Invalid, Unauthenticated, Forbidden, NotFound, Conflict,
    RequiresApprovedBindingReview, AuthorityVerificationUnavailable
}
public sealed record FiscalMemoryOperationResult(FiscalMemoryOperationStatus Status, FiscalMemoryView? Memory = null);

/// <summary>
/// Safe metadata onboarding. A taxpayer may keep one independently registered
/// Production memory and one Sandbox memory; replacements use reviewed workflows.
/// </summary>
public sealed class FiscalMemoryManagementService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService authorization,
    ParticipantScopeService scope, TaxpayerScopeService taxpayerScope)
{
    private async Task<FiscalMemoryOperationStatus?> AuthenticateLevel1Async(string? bearer, CancellationToken ct)
    {
        var actor = await authorization.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return FiscalMemoryOperationStatus.Unauthenticated;
        if (!await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == actor.Value && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct))
            return FiscalMemoryOperationStatus.Forbidden;
        return null;
    }

    private async Task<FiscalMemoryOperationStatus?> AuthenticateParticipantAsync(
        string? bearer, Guid taxpayerId, CancellationToken ct)
    {
        var principal = await taxpayerScope.ResolveAsync(bearer, ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
            return FiscalMemoryOperationStatus.Unauthenticated;
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return FiscalMemoryOperationStatus.Forbidden;
        var permitted = await taxpayerScope.ScopedQuery(principal.ActorId, principal.Level1)
            .AnyAsync(p => p.Id == taxpayerId && p.Status == TaxpayerStatus.Active, ct);
        return permitted ? null : FiscalMemoryOperationStatus.NotFound;
    }

    public async Task<FiscalMemoryOperationResult> RegisterAsync(
        string? bearer, Guid taxpayerId, RegisterFiscalMemoryRequest? request,
        CancellationToken ct = default)
    {
        var denied = await AuthenticateParticipantAsync(bearer, taxpayerId, ct);
        if (denied is { } failure) return new(failure);
        if (taxpayerId == Guid.Empty ||
             !FiscalMemoryOnboardingPolicy.TryNormalizeIdentifier(request?.MemoryIdentifier, out var identifier) ||
            request?.AuthorityEnvironment is not { } requestedEnvironment ||
            !Enum.IsDefined(requestedEnvironment))
            return new(FiscalMemoryOperationStatus.Invalid);

        // This is FIRST-TIME metadata onboarding only. The database does not yet
        // contain subscription binding state; never allow a second memory here.
        if (!await db.TaxpayerProfiles.AsNoTracking().AnyAsync(p =>
                p.Id == taxpayerId && p.Status == TaxpayerStatus.Active, ct))
            return new(FiscalMemoryOperationStatus.NotFound);

        // Serializing by the taxpayer row prevents two concurrent first-memory inserts.
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, ct);
                if (await AuthenticateParticipantAsync(bearer, taxpayerId, ct) is { } revoked)
                    return new FiscalMemoryOperationResult(revoked);
                // One identifier per environment. An unclassified legacy row occupies
                // the Production slot until its authority is explicitly verified.
                var environments = await db.FiscalMemories.AsNoTracking()
                    .Where(x => x.TaxpayerId == taxpayerId)
                    .Where(x => x.Status != FiscalMemoryStatus.Retired)
                    .Select(x => x.AuthorityEnvironment).ToArrayAsync(ct);
                var mayStageBoundProductionCandidate = requestedEnvironment == FiscalConnectionEnvironment.Production &&
                    await MayStageBoundProductionCandidateAsync(taxpayerId,ct);
                if (!FiscalMemoryOnboardingPolicy.CanRegisterEnvironment(environments, requestedEnvironment) &&
                    !mayStageBoundProductionCandidate)
                    return new FiscalMemoryOperationResult(FiscalMemoryOperationStatus.RequiresApprovedBindingReview);
                var now = DateTime.UtcNow;
                var memory = new FiscalMemory
                {
                    Id = Guid.NewGuid(), TaxpayerId = taxpayerId,
                    MemoryIdentifier = identifier,
                    AuthorityEnvironment = requestedEnvironment,
                    Status = FiscalMemoryStatus.PendingVerification,
                    Revision = 1, CreatedAtUtc = now, UpdatedAtUtc = now
                };
                db.FiscalMemories.Add(memory);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new FiscalMemoryOperationResult(FiscalMemoryOperationStatus.Success,
                    ToView(memory));
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return new(FiscalMemoryOperationStatus.Conflict);
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return new(FiscalMemoryOperationStatus.Conflict);
        }
    }

    private async Task<bool> MayStageBoundProductionCandidateAsync(Guid taxpayerId,CancellationToken ct)
    {
        var now=DateTime.UtcNow;
        var owner=await db.TaxpayerProfiles.AsNoTracking().Where(t=>t.Id==taxpayerId)
            .Select(t=>t.CustomerUserId).SingleOrDefaultAsync(ct);
        var boundMemory=await (from binding in db.AnnualSubscriptionBindings.AsNoTracking()
            join plan in db.CustomerSubscriptions.AsNoTracking() on binding.SubscriptionId equals plan.Id
            where binding.CustomerUserId==owner&&binding.TaxpayerId==taxpayerId&&plan.CustomerUserId==owner&&
                plan.State==ArginTaxPayer.Domain.Subscriptions.SubscriptionLifecycle.Active&&
                plan.ActivatedAtUtc<=now&&plan.ExpiresAtUtc>now&&
                (plan.Kind==ArginTaxPayer.Domain.Subscriptions.SubscriptionPlanKind.AnnualSingleTaxpayer||
                 plan.Kind==ArginTaxPayer.Domain.Subscriptions.SubscriptionPlanKind.MonthlySingleTaxpayer||
                 plan.Kind==ArginTaxPayer.Domain.Subscriptions.SubscriptionPlanKind.QuarterlySingleTaxpayer||
                 plan.Kind==ArginTaxPayer.Domain.Subscriptions.SubscriptionPlanKind.SemiAnnualSingleTaxpayer)
            select (Guid?)binding.FiscalMemoryId).FirstOrDefaultAsync(ct);
        if(boundMemory is null)return false;
        var pendingCandidateExists=await db.FiscalMemories.AsNoTracking().AnyAsync(m=>m.TaxpayerId==taxpayerId&&
            m.Id!=boundMemory.Value&&m.AuthorityEnvironment==FiscalConnectionEnvironment.Production&&
            m.Status!=FiscalMemoryStatus.Retired,ct);
        return !pendingCandidateExists;
    }

    public async Task<(FiscalMemoryOperationStatus Status, FiscalMemoryView[]? Memories)> ListAsync(
        string? bearer, Guid taxpayerId, CancellationToken ct = default)
    {
        var denied = await AuthenticateParticipantAsync(bearer, taxpayerId, ct);
        if (denied is { } failure) return (failure, null);
        var principal = await taxpayerScope.ResolveAsync(bearer, ct);
        var owner = await taxpayerScope.ScopedQuery(principal.ActorId, principal.Level1)
            .Where(t => t.Id == taxpayerId && t.Status == TaxpayerStatus.Active)
            .Select(t => t.CustomerUserId).SingleOrDefaultAsync(ct);
        var canVerifyAndActivate = principal.Level1 || owner == principal.ActorId;
        var list = await db.FiscalMemories.AsNoTracking()
            .Where(x => x.TaxpayerId == taxpayerId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new FiscalMemoryView(x.Id, x.TaxpayerId, x.MemoryIdentifier, x.Status, x.Revision,
                x.AuthorityEnvironment, x.LastConnectionTestSucceeded, x.LastConnectionTestAtUtc,
                canVerifyAndActivate))
            .ToArrayAsync(ct);
        return (FiscalMemoryOperationStatus.Success, list);
    }

    public async Task RecordConnectionTestAsync(string? bearer, Guid taxpayerId, Guid memoryId,
        bool succeeded, DateTime testedAtUtc, CancellationToken ct = default)
    {
        if (await AuthenticateParticipantAsync(bearer, taxpayerId, ct) is not null) return;
        await db.FiscalMemories.Where(x => x.Id == memoryId && x.TaxpayerId == taxpayerId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.LastConnectionTestSucceeded, succeeded)
                .SetProperty(x => x.LastConnectionTestAtUtc, testedAtUtc), ct);
    }

    public async Task<FiscalMemoryOperationResult> TransitionAsync(
        string? bearer, Guid taxpayerId, Guid memoryId,
        ChangeFiscalMemoryStatusRequest? request, CancellationToken ct = default)
    {
        var denied = await AuthenticateLevel1Async(bearer, ct);
        if (denied is { } failure) return new(failure);
        if (request is null || request.ExpectedRevision < 1 ||
            !Enum.IsDefined(request.Status) || taxpayerId == Guid.Empty || memoryId == Guid.Empty)
            return new(FiscalMemoryOperationStatus.Invalid);
        var memory = await db.FiscalMemories.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == memoryId && m.TaxpayerId == taxpayerId, ct);
        if (memory is null) return new(FiscalMemoryOperationStatus.NotFound);
        if (memory.Revision != request.ExpectedRevision)
            return new(FiscalMemoryOperationStatus.Conflict);
        if (!TaxpayerLifecyclePolicy.CanTransition(memory.Status, request.Status))
            return new(FiscalMemoryOperationStatus.Invalid);

        // Pending -> Retired is only safe while never activated. It does not
        // replace a bound memory and cannot enable an alternative one.
        if (!FiscalMemoryOnboardingPolicy.CanLocallyRetireNeverActivatedMemory(
                memory.Status, request.Status))
            return new(FiscalMemoryOperationStatus.AuthorityVerificationUnavailable);

        var now = DateTime.UtcNow;
        var changed = await db.FiscalMemories.Where(m => m.Id == memoryId &&
                m.TaxpayerId == taxpayerId && m.Revision == request.ExpectedRevision &&
                m.Status == FiscalMemoryStatus.PendingVerification)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, FiscalMemoryStatus.Retired)
                .SetProperty(x => x.Revision, x => x.Revision + 1)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);
        if (changed != 1) return new(FiscalMemoryOperationStatus.Conflict);
        return new(FiscalMemoryOperationStatus.Success,
            new FiscalMemoryView(memory.Id, taxpayerId, memory.MemoryIdentifier,
                FiscalMemoryStatus.Retired, memory.Revision + 1, memory.AuthorityEnvironment));
    }

    private static FiscalMemoryView ToView(FiscalMemory memory) =>
        new(memory.Id, memory.TaxpayerId, memory.MemoryIdentifier, memory.Status, memory.Revision,
            memory.AuthorityEnvironment, memory.LastConnectionTestSucceeded, memory.LastConnectionTestAtUtc);
}
