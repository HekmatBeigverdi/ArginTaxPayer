using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public sealed record RegisterFiscalMemoryRequest(string? MemoryIdentifier);
public sealed record ChangeFiscalMemoryStatusRequest(FiscalMemoryStatus Status, long ExpectedRevision);
public sealed record FiscalMemoryView(Guid Id, Guid TaxpayerId, string MemoryIdentifier,
    FiscalMemoryStatus Status, long Revision);
public enum FiscalMemoryOperationStatus
{
    Success, Invalid, Unauthenticated, Forbidden, NotFound, Conflict,
    RequiresApprovedBindingReview, AuthorityVerificationUnavailable
}
public sealed record FiscalMemoryOperationResult(FiscalMemoryOperationStatus Status, FiscalMemoryView? Memory = null);

/// <summary>
/// Phase 04 onboarding-only safe API. Annual binding is not yet persisted (Phase 12).
/// Until binding and authoritative connection verification exist, no API can activate,
/// reactivate, suspend or retire an active fiscal memory or add a second memory.
/// </summary>
public sealed class FiscalMemoryManagementService(
    ArginTaxPayerDbContext db, IdentityAuthorizationService authorization)
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

    public async Task<FiscalMemoryOperationResult> RegisterAsync(
        string? bearer, Guid taxpayerId, RegisterFiscalMemoryRequest? request,
        CancellationToken ct = default)
    {
        var denied = await AuthenticateLevel1Async(bearer, ct);
        if (denied is { } failure) return new(failure);
        if (taxpayerId == Guid.Empty ||
            !FiscalMemoryOnboardingPolicy.TryNormalizeIdentifier(request?.MemoryIdentifier, out var identifier))
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
                // Recheck inside transaction; no implicit read access for Level 2.
                if (await db.FiscalMemories.AsNoTracking().AnyAsync(x => x.TaxpayerId == taxpayerId, ct))
                    return new FiscalMemoryOperationResult(FiscalMemoryOperationStatus.RequiresApprovedBindingReview);
                var now = DateTime.UtcNow;
                var memory = new FiscalMemory
                {
                    Id = Guid.NewGuid(), TaxpayerId = taxpayerId,
                    MemoryIdentifier = identifier,
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

    public async Task<(FiscalMemoryOperationStatus Status, FiscalMemoryView[]? Memories)> ListAsync(
        string? bearer, Guid taxpayerId, CancellationToken ct = default)
    {
        var denied = await AuthenticateLevel1Async(bearer, ct);
        if (denied is { } failure) return (failure, null);
        if (!await db.TaxpayerProfiles.AsNoTracking().AnyAsync(p => p.Id == taxpayerId, ct))
            return (FiscalMemoryOperationStatus.NotFound, null);
        var list = await db.FiscalMemories.AsNoTracking()
            .Where(x => x.TaxpayerId == taxpayerId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new FiscalMemoryView(x.Id, x.TaxpayerId, x.MemoryIdentifier, x.Status, x.Revision))
            .ToArrayAsync(ct);
        return (FiscalMemoryOperationStatus.Success, list);
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
                FiscalMemoryStatus.Retired, memory.Revision + 1));
    }

    private static FiscalMemoryView ToView(FiscalMemory memory) =>
        new(memory.Id, memory.TaxpayerId, memory.MemoryIdentifier, memory.Status, memory.Revision);
}
