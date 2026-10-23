using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Import;

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
