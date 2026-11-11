using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

public sealed record ClassifyFiscalMemoryAuthorityRequest(FiscalConnectionEnvironment AuthorityEnvironment, long ExpectedRevision);
public sealed record VerifyFiscalMemoryRequest(long ExpectedRevision);
public sealed record FiscalMemoryAuthorityResult(int StatusCode, string Code, FiscalMemoryView? Memory = null);

public static class FiscalMemoryAuthorityVerificationPolicy
{
    // Archived SDK 0.0.30 Enums/FiscalStatus.cs: ACTIVE=0, INACTIVE=1.
    public static bool CanActivate(SigningConnectionTestResult result, string taxpayerIdentifier) =>
        result.Status == SigningConnectionTestStatus.Authenticated && result.AuthenticationConfirmed &&
        result.FiscalInformation is { } info &&
        info.FiscalStatus is "ACTIVE" or "0" &&
        !string.IsNullOrWhiteSpace(taxpayerIdentifier) &&
        string.Equals(info.NationalId, taxpayerIdentifier, StringComparison.Ordinal);
}

/// <summary>Authority classification and activation, based on authenticated authority responses.</summary>
public sealed class FiscalMemoryAuthorityOnboardingService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope,
    ISigningReadOnlyConnectionTest connection, InvoiceSubscriptionGate subscriptions)
{
    private async Task<(int Code, Guid Actor, Guid Owner)> AuthorizeAsync(string? bearer, Guid taxpayerId, CancellationToken ct)
    {
        var principal = await scope.ResolveAsync(bearer, ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated) return (401, default, default);
        if (principal.Decision != TaxpayerScopeDecision.Allowed || !principal.Level1) return (403, default, default);
        if (!await db.IdentityMfaStates.AsNoTracking().AnyAsync(m =>
                m.UserId == principal.ActorId && m.EnabledAtUtc != null, ct)) return (403, default, default);
        var owner = await scope.ScopedCommandQuery(principal.ActorId, principal.Level1)
            .Where(t => t.Id == taxpayerId && t.Status == TaxpayerStatus.Active)
            .Select(t => t.CustomerUserId).SingleOrDefaultAsync(ct);
        if (owner is null || owner == Guid.Empty) return (404, default, default);
        if (!await db.CustomerTaxpayerMemberships.AsNoTracking().AnyAsync(m =>
                m.CustomerUserId == owner && m.TaxpayerId == taxpayerId && m.UnlinkedAtUtc == null, ct))
            return (403, default, default);
        return (200, principal.ActorId, owner.Value);
    }

    private async Task<(int Code, Guid Actor, Guid Owner)> AuthorizeActivationAsync(
        string? bearer, Guid taxpayerId, CancellationToken ct)
    {
        var principal = await scope.ResolveAsync(bearer, ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated) return (401, default, default);
        if (principal.Decision != TaxpayerScopeDecision.Allowed) return (403, default, default);
        var taxpayer = await scope.ScopedCommandQuery(principal.ActorId, principal.Level1)
            .Where(t => t.Id == taxpayerId && t.Status == TaxpayerStatus.Active)
            .Select(t => new { t.CustomerUserId }).SingleOrDefaultAsync(ct);
        if (taxpayer?.CustomerUserId is not { } owner || owner == Guid.Empty) return (404, default, default);
        if (!await db.CustomerTaxpayerMemberships.AsNoTracking().AnyAsync(m =>
                m.CustomerUserId == owner && m.TaxpayerId == taxpayerId && m.UnlinkedAtUtc == null, ct))
            return (403, default, default);
        // The customer who owns this taxpayer can complete onboarding without a
        // manager dashboard. Delegated Level 2 users cannot activate memories.
        if (principal.ActorId != owner && !principal.Level1) return (403, default, default);
        return (200, principal.ActorId, owner);
    }

    public async Task<FiscalMemoryAuthorityResult> ClassifyAsync(string? bearer, Guid taxpayerId, Guid memoryId,
        ClassifyFiscalMemoryAuthorityRequest request, CancellationToken ct = default)
    {
        if (request.ExpectedRevision < 1 || !Enum.IsDefined(request.AuthorityEnvironment))
            return new(400, "invalid_memory_environment");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var actor = await AuthorizeAsync(bearer, taxpayerId, ct);
            if (actor.Code != 200) return new FiscalMemoryAuthorityResult(actor.Code, "memory_scope_denied");
            var memory = await db.FiscalMemories.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memoryId && m.TaxpayerId == taxpayerId, ct);
            if (memory is null) return new(404, "memory_not_found");
            if (memory.AuthorityEnvironment is not null || memory.Revision != request.ExpectedRevision)
                return new(409, "memory_already_classified_or_changed");
            // Historical attempted/unclassified dispatches must be reconciled, never relabeled.
            if (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM invoice_dispatch_operations WHERE fiscal_memory_id={memoryId}").SingleAsync(ct) != 0)
                return new(409, "memory_dispatch_history_requires_review");
            var now = DateTime.UtcNow;
            var changed = await db.FiscalMemories.Where(m => m.Id == memoryId && m.TaxpayerId == taxpayerId &&
                    m.AuthorityEnvironment == null && m.Revision == request.ExpectedRevision)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.AuthorityEnvironment, request.AuthorityEnvironment)
                    .SetProperty(m => m.Revision, m => m.Revision + 1).SetProperty(m => m.UpdatedAtUtc, now), ct);
            if (changed != 1) return new(409, "stale_memory_revision");
            await AuditAsync(memory, actor.Actor, request.AuthorityEnvironment, "classify", memory.Status, now, ct);
            await tx.CommitAsync(ct);
            return new(200, "memory_authority_classified", new(memory.Id, taxpayerId, memory.MemoryIdentifier,
                memory.Status, memory.Revision + 1, request.AuthorityEnvironment));
        });
    }

    public async Task<bool> ActivateAfterSuccessfulConnectionTestAsync(string? bearer,
        Guid taxpayerId, Guid memoryId, FiscalConnectionEnvironment? testedEnvironment,
        SigningConnectionTestResult result, CancellationToken ct = default, ConnectionBinding? expectedBinding = null)
    {
        if (result.Status != SigningConnectionTestStatus.Authenticated || expectedBinding is null) return false;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var actor = await AuthorizeActivationAsync(bearer, taxpayerId, ct);
            if (actor.Code != 200) return false;
            var currentBinding=await new FiscalMemoryConnectionHistory(db).CaptureRegisteredAsync(taxpayerId,memoryId,ct);
            if(expectedBinding.Source!="RegisteredMemory" || expectedBinding.TaxpayerId!=taxpayerId || expectedBinding.MemoryId!=memoryId ||
                expectedBinding.OwnerId!=actor.Owner || !ConnectionHistoryPolicy.CanRecord(expectedBinding,currentBinding))return false;
            var taxpayerIdentifier = await db.TaxpayerProfiles.AsNoTracking()
                .Where(t => t.Id == taxpayerId && t.Status == TaxpayerStatus.Active)
                .Select(t => t.NormalizedIdentifier).SingleOrDefaultAsync(ct);
            if (taxpayerIdentifier is null ||
                !FiscalMemoryAuthorityVerificationPolicy.CanActivate(result, taxpayerIdentifier)) return false;
            var memory = await db.FiscalMemories.AsNoTracking()
                .SingleOrDefaultAsync(m => m.Id == memoryId && m.TaxpayerId == taxpayerId, ct);
            if (memory is null) return false;
            var environment = memory.AuthorityEnvironment ?? testedEnvironment;
            if (environment is null || !Enum.IsDefined(environment.Value) ||
                testedEnvironment is { } selected && selected != environment) return false;
            if (memory.Status == FiscalMemoryStatus.Active)
            {
                await tx.CommitAsync(ct);
                return true;
            }
            if (memory.Status != FiscalMemoryStatus.PendingVerification) return false;
            if (await SelectedKeyAsync(actor.Owner, taxpayerId, memoryId, ct) is null) return false;
            if (await subscriptions.CheckAsync(actor.Owner, ct) is not null) return false;
            var classify = memory.AuthorityEnvironment is null;
            if (classify && await db.Database.SqlQuery<int>($"""
                    SELECT count(*)::int AS "Value" FROM invoice_dispatch_operations WHERE fiscal_memory_id={memoryId}
                    """).SingleAsync(ct) != 0) return false;
            var now = result.TestedAtUtc;
            var changed = await db.FiscalMemories.Where(m => m.Id == memoryId && m.TaxpayerId == taxpayerId &&
                    m.AuthorityEnvironment == memory.AuthorityEnvironment &&
                    m.Status == FiscalMemoryStatus.PendingVerification &&
                    m.Revision == memory.Revision)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(m => m.AuthorityEnvironment, environment.Value)
                    .SetProperty(m => m.Status, FiscalMemoryStatus.Active)
                    .SetProperty(m => m.Revision, m => m.Revision + (classify ? 2 : 1))
                    .SetProperty(m => m.UpdatedAtUtc, now), ct);
            if (changed != 1) return false;
            if (classify)
            {
                await AuditAsync(memory, actor.Actor, environment.Value, "classify", memory.Status, now, ct);
                memory.AuthorityEnvironment = environment;
                memory.Revision++;
            }
            await AuditAsync(memory, actor.Actor, environment.Value, "activate", FiscalMemoryStatus.Active, now, ct);
            await tx.CommitAsync(ct);
            return true;
        });
    }

    public async Task<FiscalMemoryAuthorityResult> VerifyAndActivateAsync(string? bearer, Guid taxpayerId, Guid memoryId,
        VerifyFiscalMemoryRequest request, CancellationToken ct = default)
    {
        if (request.ExpectedRevision < 1) return new(400, "invalid_memory_revision");
        var actor = await AuthorizeActivationAsync(bearer, taxpayerId, ct);
        if (actor.Code != 200) return new(actor.Code, "memory_scope_denied");
        var memory = await db.FiscalMemories.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memoryId && m.TaxpayerId == taxpayerId, ct);
        if (memory is null) return new(404, "memory_not_found");
        if (memory.AuthorityEnvironment is not { } environment || !Enum.IsDefined(environment))
            return new(409, "memory_environment_unclassified");
        if (memory.Revision != request.ExpectedRevision || memory.Status != FiscalMemoryStatus.PendingVerification)
            return new(409, "memory_not_pending_or_changed");
        var identifier = await db.TaxpayerProfiles.AsNoTracking().Where(t => t.Id == taxpayerId)
            .Select(t => t.NormalizedIdentifier).SingleAsync(ct);
        var keyId = await SelectedKeyAsync(actor.Owner, taxpayerId, memoryId, ct);
        if (keyId is null) return new(409, "key_not_ready");
        var subscriptionBlock = await subscriptions.CheckAsync(actor.Owner, ct);
        if (subscriptionBlock is not null) return new(409, subscriptionBlock);
        // Deliberately outside the retried DB transaction: a retry cannot repeat authority IO.
        var result = await connection.TestAsync(bearer, taxpayerId, memoryId, ct);
        if (!FiscalMemoryAuthorityVerificationPolicy.CanActivate(result, identifier))
            return new(422, result.AuthenticationConfirmed ? "authority_memory_inactive_or_taxpayer_mismatch" : result.SafeCode);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var current = await AuthorizeActivationAsync(bearer, taxpayerId, ct);
            if (current.Code != 200 || current.Owner != actor.Owner)
                return new FiscalMemoryAuthorityResult(current.Code == 200 ? 409 : current.Code, "memory_scope_changed");
            if (await SelectedKeyAsync(current.Owner, taxpayerId, memoryId, ct) != keyId ||
                !await db.TaxpayerProfiles.AsNoTracking().AnyAsync(t => t.Id == taxpayerId && t.NormalizedIdentifier == identifier, ct))
                return new(409, "memory_verification_scope_changed");
            var blocked = await subscriptions.CheckAsync(current.Owner, ct);
            if (blocked is not null) return new(409, blocked);
            var now = DateTime.UtcNow;
            var changed = await db.FiscalMemories.Where(m => m.Id == memoryId && m.TaxpayerId == taxpayerId &&
                    m.Revision == request.ExpectedRevision && m.AuthorityEnvironment == environment &&
                    m.Status == FiscalMemoryStatus.PendingVerification)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, FiscalMemoryStatus.Active)
                    .SetProperty(m => m.Revision, m => m.Revision + 1).SetProperty(m => m.UpdatedAtUtc, now), ct);
            if (changed != 1) return new(409, "stale_memory_revision");
            await AuditAsync(memory, current.Actor, environment, "activate", FiscalMemoryStatus.Active, now, ct);
            await tx.CommitAsync(ct);
            return new(200, "authority_verified_memory_active", new(memory.Id, taxpayerId, memory.MemoryIdentifier,
                FiscalMemoryStatus.Active, memory.Revision + 1, environment));
        });
    }

    private async Task<Guid?> SelectedKeyAsync(Guid owner, Guid taxpayer, Guid memory, CancellationToken ct)
    {
        var selected = await db.SigningKeySelections.AsNoTracking().Where(s =>
            s.CustomerUserId == owner && s.TaxpayerId == taxpayer && s.FiscalMemoryId == memory)
            .Select(s => (Guid?)s.KeyVersionId).SingleOrDefaultAsync(ct);
        var keys = db.SigningKeyVersions.AsNoTracking().Where(k => k.CustomerUserId == owner &&
            k.TaxpayerId == taxpayer && k.FiscalMemoryId == memory && k.LifecycleState == 2 && k.PublicSpkiDer != null);
        return selected is { } id ? await keys.Where(k => k.Id == id).Select(k => (Guid?)k.Id).SingleOrDefaultAsync(ct)
            : await keys.OrderByDescending(k => k.VersionNumber).Select(k => (Guid?)k.Id).FirstOrDefaultAsync(ct);
    }

    private Task<int> AuditAsync(FiscalMemory memory, Guid actor, FiscalConnectionEnvironment environment,
        string action, FiscalMemoryStatus nextStatus, DateTime now, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO fiscal_memory_authority_events
              (id,fiscal_memory_id,taxpayer_id,actor_user_id,authority_environment,action,
               from_status,to_status,memory_revision,recorded_at_utc)
            VALUES ({Guid.NewGuid()},{memory.Id},{memory.TaxpayerId},{actor},{(int)environment},{action},
                    {(int)memory.Status},{(int)nextStatus},{memory.Revision + 1},{now})
            """, ct);
}
