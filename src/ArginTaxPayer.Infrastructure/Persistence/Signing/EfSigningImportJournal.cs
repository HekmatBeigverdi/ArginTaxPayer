using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Import;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed partial class EfSigningOperationRepository : ISigningImportJournal
{
    async Task<GenerationReservation> ISigningImportJournal.AuthorizeImportAsync(string? bearer,
        ImportSigningKeyRequest request, CancellationToken ct)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(SigningGenerationStatus.Unauthenticated, default, Guid.Empty, default);
        var scope = new SigningScope(actor.Value, request.TaxpayerId, request.FiscalMemoryId);
        if (await AuthorizeAsync(bearer, scope, true, ct) is null ||
            await RecentLoginAsync(bearer, actor.Value, DateTime.UtcNow, ct) is null)
            return new(SigningGenerationStatus.Forbidden, default, Guid.Empty, default);
        return new(SigningGenerationStatus.Replayed, scope, Guid.Empty, default);
    }

    async Task<ExistingKeyImportTarget?> ISigningImportJournal.FindSameKeyTargetAsync(
        string? bearer, ImportSigningKeyRequest request, string publicFingerprint, CancellationToken ct)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return null;
        var scope = new SigningScope(actor.Value, request.TaxpayerId, request.FiscalMemoryId);
        if (await AuthorizeAsync(bearer, scope, true, ct) is null ||
            await RecentLoginAsync(bearer, actor.Value, DateTime.UtcNow, ct) is null)
            return null;

        var selectedKeyId = await db.SigningKeySelections.AsNoTracking().Where(s =>
                s.CustomerUserId == actor.Value && s.TaxpayerId == request.TaxpayerId &&
                s.FiscalMemoryId == request.FiscalMemoryId)
            .Select(s => s.KeyVersionId).SingleOrDefaultAsync(ct);
        var keys = db.SigningKeyVersions.AsNoTracking().Where(k =>
            k.CustomerUserId == actor.Value && k.TaxpayerId == request.TaxpayerId &&
            k.FiscalMemoryId == request.FiscalMemoryId && k.LifecycleState == 2 &&
            k.ProfileId == SigningOperationPolicy.LegacyProfile && k.PublicSpkiDer != null);
        var current = selectedKeyId is { } selected
            ? await keys.SingleOrDefaultAsync(k => k.Id == selected, ct)
            : await keys.OrderByDescending(k => k.VersionNumber).FirstOrDefaultAsync(ct);
        if (current is null || current.PublicFingerprintSha256 != publicFingerprint ||
            current.OriginOperationRecordId == Guid.Empty) return null;

        var result = await ((ISigningGenerationJournal)this).ReadGeneratedAsync(bearer, scope,
            current.Id, current.OriginOperationRecordId, ct);
        if (result.Status != SigningGenerationStatus.Replayed || result.PublicKey is null) return null;
        return new(new(actor.Value, request.TaxpayerId, request.FiscalMemoryId,
            current.Id, current.OriginOperationRecordId), result);
    }

    async Task<GenerationReservation> ISigningImportJournal.ReserveImportAsync(string? bearer,
        ImportSigningKeyRequest request, string publicFingerprint, CancellationToken ct)
    {
        var authorized = await ((ISigningImportJournal)this).AuthorizeImportAsync(bearer, request, ct);
        if (authorized.Status != SigningGenerationStatus.Replayed) return authorized;
        var result = await ReserveAsync(bearer, new(authorized.Scope, request.OperationId, SigningOperationKind.Import,
            request.ProfileId, request.ConsentVersion, request.AcceptServerCustody, publicFingerprint), ct);
        return new(GenerationStatus(result.Status), authorized.Scope, result.Operation?.RecordId ?? Guid.Empty,
            result.Operation?.State ?? default);
    }

    Task<GenerationAdmission> ISigningImportJournal.ClaimImportAsync(string? bearer,
        GenerationReservation reservation, Guid operationId, string publicFingerprint, CancellationToken ct) =>
        ClaimKeyOperationAsync(bearer, reservation, operationId, SigningOperationKind.Import, publicFingerprint, ct);

    Task<SigningKeyGenerationResult> ISigningImportJournal.CompleteImportAsync(string? bearer,
        Guid operationId, SealedGeneratedKey receipt, CancellationToken ct) =>
        CompleteKeyOperationAsync(bearer, operationId, receipt, SigningOperationKind.Import, ct);

    Task<SigningKeyGenerationResult> ISigningImportJournal.ReadImportAsync(string? bearer,
        GenerationReservation reservation, CancellationToken ct) =>
        ((ISigningGenerationJournal)this).ReadGeneratedAsync(bearer, reservation.Scope, Guid.Empty,
            reservation.OperationRecordId, ct);
}
