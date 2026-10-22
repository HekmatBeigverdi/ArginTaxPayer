using System.Data;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Signing.Review;

/// <summary>Level1 + active MFA review. No private key access, key rotation or memory mutation.</summary>
public sealed class EfSigningAdminReviewWorkflow(ArginTaxPayerDbContext db) : ISigningAdminReviewWorkflow
{
    private readonly IdentitySessionService sessions = new(db);
    private async Task<Guid?> AdminAsync(string? bearer, CancellationToken ct)
    {
        var id = await sessions.AuthenticateAsync(bearer, ct);
        if (id is null) return null;
        return await db.IdentityUsers.AsNoTracking().AnyAsync(u =>
            u.Id == id.Value && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(m => m.UserId == u.Id && m.EnabledAtUtc != null), ct)
            ? id : null;
    }

    private async Task<SigningAdminChangeRequest> ViewAsync(SigningChangeRequest r, CancellationToken ct)
    {
        var title = await db.TaxpayerProfiles.AsNoTracking().Where(t => t.Id == r.TaxpayerId)
            .Select(t => t.DisplayName).FirstOrDefaultAsync(ct) ?? "مؤدی";
        var memory = await db.FiscalMemories.AsNoTracking().Where(m => m.Id == r.FiscalMemoryId)
            .Select(m => m.MemoryIdentifier).FirstOrDefaultAsync(ct) ?? "";
        return new(r.Id, r.CustomerUserId, r.TaxpayerId, title, r.FiscalMemoryId, memory,
            r.CurrentKeyVersionId, r.ExpectedKeyRevision, (SigningChangeKind)r.Kind,
            r.ReasonCode, r.Explanation, (SigningChangeReviewState)r.State,
            r.RequestedByUserId, r.RequestedAtUtc, r.Revision, r.ReviewedAtUtc,
            r.ExpiresAtUtc, r.DecisionReason);
    }

    public async Task<IReadOnlyList<SigningAdminChangeRequest>?> ListPendingAsync(string? bearer,
        CancellationToken ct = default)
    {
        if (await AdminAsync(bearer, ct) is null) return null;
        var records = await db.SigningChangeRequests.AsNoTracking().Where(r => r.State == 1)
            .OrderBy(r => r.RequestedAtUtc).Take(100).ToListAsync(ct);
        var result = new List<SigningAdminChangeRequest>(records.Count);
        foreach (var r in records) result.Add(await ViewAsync(r, ct));
        return result;
    }

    public async Task<SigningAdminReviewResult> DecideAsync(string? bearer,
        SigningAdminReviewDecision decision, CancellationToken ct = default)
    {
        if (decision is null || decision.RequestId == Guid.Empty || decision.ExpectedRevision < 1 ||
            string.IsNullOrWhiteSpace(decision.DecisionReason) ||
            decision.DecisionReason.Trim().Length is < 10 or > 1000 ||
            decision.DecisionReason.Any(char.IsControl))
            return new(SigningAdminReviewStatus.Invalid);
        var admin = await AdminAsync(bearer, ct);
        if (admin is null) return new(SigningAdminReviewStatus.Forbidden);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (await AdminAsync(bearer, ct) != admin)
                    return new SigningAdminReviewResult(SigningAdminReviewStatus.Forbidden);
                var req = await db.SigningChangeRequests.SingleOrDefaultAsync(r => r.Id == decision.RequestId, ct);
                if (req is null) return new(SigningAdminReviewStatus.NotFound);
                if (req.RequestedByUserId == admin || req.State != 1 ||
                    req.Revision != decision.ExpectedRevision)
                    return new(SigningAdminReviewStatus.Conflict);
                var key = await db.SigningKeyVersions.AsNoTracking().SingleOrDefaultAsync(k =>
                    k.Id == req.CurrentKeyVersionId &&
                    k.CustomerUserId == req.CustomerUserId &&
                    k.TaxpayerId == req.TaxpayerId &&
                    k.FiscalMemoryId == req.FiscalMemoryId, ct);
                if (key is null || key.LifecycleState != 2 || key.Revision != req.ExpectedKeyRevision)
                    return new(SigningAdminReviewStatus.Conflict);

                var now = DateTime.UtcNow;
                req.State = (short)(decision.Approve ? SigningChangeReviewState.Approved : SigningChangeReviewState.Rejected);
                req.ReviewedByUserId = admin.Value;
                req.ReviewedAtUtc = now;
                req.ExpiresAtUtc = decision.Approve ? now.AddHours(24) : null;
                req.DecisionReason = decision.DecisionReason.Trim();
                req.Revision++;

                db.SigningAuditEvents.Add(new SigningAuditEvent {
                    Id = Guid.NewGuid(), CustomerUserId = req.CustomerUserId,
                    TaxpayerId = req.TaxpayerId, FiscalMemoryId = req.FiscalMemoryId,
                    OperationRecordId = key.OriginOperationRecordId, ActorUserId = admin.Value,
                    SubjectKind = 4, SubjectId = req.Id, Revision = req.Revision,
                    ActionCode = decision.Approve ? "change_request_approved" : "change_request_rejected",
                    OutcomeCode = decision.Approve ? "Approved" : "Rejected",
                    PublicFingerprintSha256 = key.PublicFingerprintSha256, OccurredAtUtc = now
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new SigningAdminReviewResult(SigningAdminReviewStatus.Updated,
                    await ViewAsync(req, ct));
            });
        }
        catch (DbUpdateConcurrencyException) { return new(SigningAdminReviewStatus.Conflict); }
        catch (DbUpdateException) { return new(SigningAdminReviewStatus.Conflict); }
        catch (PostgresException ex) when (ex.SqlState is "40001" or "23505")
        { return new(SigningAdminReviewStatus.Conflict); }
    }
}
