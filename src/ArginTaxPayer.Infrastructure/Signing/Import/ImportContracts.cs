using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;

namespace ArginTaxPayer.Infrastructure.Signing.Import;

internal interface ISigningImportJournal
{
    Task<GenerationReservation> AuthorizeImportAsync(string? bearer, ImportSigningKeyRequest request, CancellationToken ct);
    Task<GenerationReservation> ReserveImportAsync(string? bearer, ImportSigningKeyRequest request,
        string publicFingerprint, CancellationToken ct);
    Task<GenerationAdmission> ClaimImportAsync(string? bearer, GenerationReservation reservation,
        Guid operationId, string publicFingerprint, CancellationToken ct);
    Task<SigningKeyGenerationResult> CompleteImportAsync(string? bearer, Guid operationId,
        SealedGeneratedKey receipt, CancellationToken ct);
    Task<SigningKeyGenerationResult> ReadImportAsync(string? bearer, GenerationReservation reservation, CancellationToken ct);
}
