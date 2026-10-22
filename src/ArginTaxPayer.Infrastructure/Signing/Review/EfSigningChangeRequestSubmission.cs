using System.Data;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Signing.Review;

/// <summary>Step23: request only. No approval, memory reassignment, key mutation or invoice dispatch.</summary>
internal sealed class EfSigningChangeRequestSubmission(ArginTaxPayerDbContext db,
    EfSigningOperationRepository authorization) : ISigningChangeRequestSubmission
{
    private static SigningChangeRequestView View(SigningChangeRequest r) =>
        new(r.Id, r.FiscalMemoryId, (SigningChangeKind)r.Kind, r.ReasonCode,
            r.Explanation, (SigningChangeReviewState)r.State,
            r.ExpectedKeyRevision, r.RequestedAtUtc, r.Revision);

    private async Task<Guid?> OwnerAsync(string? bearer, Guid taxpayerId, Guid memoryId, CancellationToken ct)
    {
        var auth = await authorization.ReadImportOnboardingAsync(bearer, taxpayerId, memoryId, ct);
        if (auth.Status != SigningGenerationStatus.Replayed) return null;
        var owner = await db.TaxpayerProfiles.AsNoTracking()
            .Where(p => p.Id == taxpayerId).Select(p => p.CustomerUserId).SingleOrDefaultAsync(ct);
        return owner == Guid.Empty ? null : owner;
    }

    public async Task<IReadOnlyList<SigningChangeRequestView>?> ListAsync(string? bearer,
        Guid taxpayerId, Guid memoryId, CancellationToken ct = default)
    {
        var owner = await OwnerAsync(bearer, taxpayerId, memoryId, ct);
        if (owner is null) return null;
        var result = await db.SigningChangeRequests.AsNoTracking().Where(r =>
            r.CustomerUserId == owner && r.TaxpayerId == taxpayerId &&
            r.FiscalMemoryId == memoryId).OrderByDescending(r => r.RequestedAtUtc)
            .Take(30).ToListAsync(ct);
        return result.Select(View).ToArray();
    }

    public async Task<SigningChangeSubmission> SubmitAsync(string? bearer, Guid taxpayerId,
        Guid memoryId, SigningChangeRequestInput input, CancellationToken ct = default)
    {
        if (!SigningChangeSubmissionPolicy.Valid(input) || taxpayerId == Guid.Empty ||
            memoryId == Guid.Empty) return new(SigningChangeSubmissionStatus.Invalid);
        var owner = await OwnerAsync(bearer, taxpayerId, memoryId, ct);
        if (owner is null) return new(SigningChangeSubmissionStatus.Forbidden);
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var existing = await db.SigningChangeRequests.AsNoTracking().SingleOrDefaultAsync(r =>
                r.Id == input.RequestId, ct);
            if (existing is not null)
            {
                if (existing.CustomerUserId != owner || existing.TaxpayerId != taxpayerId ||
                    existing.FiscalMemoryId != memoryId || existing.Kind != (short)input.Kind ||
                    existing.ExpectedKeyRevision != input.ExpectedKeyRevision ||
                    existing.ReasonCode != input.ReasonCode || existing.Explanation != input.Explanation)
                    return new(SigningChangeSubmissionStatus.Conflict);
                return new(SigningChangeSubmissionStatus.Replayed, View(existing));
            }
            // Phase12 entitlement is not authoritative yet. Never grant a second memory from this
            // endpoint; only a request for the currently sole registered memory is admitted.
            var memories = await db.FiscalMemories.AsNoTracking()
                .Where(m => m.TaxpayerId == taxpayerId).Select(m => m.Id).Take(2).ToListAsync(ct);
            if (memories.Count != 1 || memories[0] != memoryId)
                return new(SigningChangeSubmissionStatus.SecondMemoryDenied);
            var key = await db.SigningKeyVersions.AsNoTracking().Where(k =>
                k.CustomerUserId == owner && k.TaxpayerId == taxpayerId &&
                k.FiscalMemoryId == memoryId && k.LifecycleState == 2)
                .OrderByDescending(k => k.VersionNumber).FirstOrDefaultAsync(ct);
            if (key is null) return new(SigningChangeSubmissionStatus.KeyNotReady);
            if (key.Revision != input.ExpectedKeyRevision)
                return new(SigningChangeSubmissionStatus.Conflict);
            if (await db.SigningChangeRequests.AnyAsync(r =>
                r.CustomerUserId == owner && r.TaxpayerId == taxpayerId &&
                r.FiscalMemoryId == memoryId && (r.State == 1 || r.State == 2), ct))
                return new(SigningChangeSubmissionStatus.Conflict);

            var record = new SigningChangeRequest
            {
                Id = input.RequestId, CustomerUserId = owner.Value,
                TaxpayerId = taxpayerId, FiscalMemoryId = memoryId,
                CurrentKeyVersionId = key.Id, ExpectedKeyRevision = key.Revision,
                Kind = (short)input.Kind, State = 1, ReasonCode = input.ReasonCode,
                Explanation = input.Explanation, RequestedByUserId = owner.Value,
                RequestedAtUtc = DateTime.UtcNow, Revision = 1
            };
            db.SigningChangeRequests.Add(record);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(SigningChangeSubmissionStatus.Created, View(record));
            });
        }
        catch (DbUpdateException) { return new(SigningChangeSubmissionStatus.Conflict); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState is "40001" or "23505")
        { return new(SigningChangeSubmissionStatus.Conflict); }
    }
}
