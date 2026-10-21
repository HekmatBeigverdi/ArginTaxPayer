using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

internal sealed record GenerationReservation(SigningGenerationStatus Status, SigningScope Scope,
    Guid OperationRecordId, SigningOperationState State);
internal sealed record GenerationAdmission(SigningGenerationStatus Status,
    KeyProtectionIdentity? Identity = null, bool OwnsClaim = false);
internal sealed record SealedGeneratedKey(KeyProtectionIdentity Identity,
    byte[] PublicSpkiDer, string PublicFingerprintSha256, string ProtectionKeyVersion);

internal interface ISigningGenerationJournal
{
    Task<GenerationReservation> ReserveGenerationAsync(string? bearer, GenerateSigningKeyRequest request, CancellationToken ct);
    Task<GenerationAdmission> ClaimGenerationAsync(string? bearer, GenerationReservation reservation, Guid operationId, CancellationToken ct);
    Task<SigningKeyGenerationResult> CompleteGenerationAsync(string? bearer, Guid operationId,
        SealedGeneratedKey receipt, CancellationToken ct);
    Task<SigningKeyGenerationResult> ReadGeneratedAsync(string? bearer, SigningScope scope, Guid keyId,
        Guid? operationRecordId, CancellationToken ct);
    Task<SigningKeyGenerationResult> ReadPublicAsync(string? bearer, Guid taxpayerId, Guid memoryId, Guid keyId, CancellationToken ct);
}

internal interface IGeneratedKeyFactory { GeneratedRsaKey Create(); }
internal interface IGeneratedKeyCustody
{
    Task EnsureReadyAsync(CancellationToken ct);
    Task<SealedGeneratedKey> StageAsync(KeyProtectionIdentity id, GeneratedRsaKey key, CancellationToken ct);
    Task<SealedGeneratedKey?> RecoverAsync(KeyProtectionIdentity id, CancellationToken ct);
}
