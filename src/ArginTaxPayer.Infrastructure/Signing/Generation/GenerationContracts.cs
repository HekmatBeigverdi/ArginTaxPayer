using ArginTaxPayer.Application.Signing;
using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

internal sealed record GenerationReservation(SigningGenerationStatus Status, SigningScope Scope,
    Guid OperationRecordId, SigningOperationState State);
internal sealed record GenerationAdmission(SigningGenerationStatus Status,
    KeyProtectionIdentity? Identity = null, bool OwnsClaim = false);
internal sealed record SealedGeneratedKey(KeyProtectionIdentity Identity,
    byte[] PublicSpkiDer, string PublicFingerprintSha256, string ProtectionKeyVersion);

internal sealed class GeneratedKeyHandoff : IDisposable
{
    private readonly SensitiveBytes privateMaterial;
    private readonly byte[] publicSpkiDer;

    private GeneratedKeyHandoff(SensitiveBytes privateMaterial, byte[] publicSpkiDer)
    {
        this.privateMaterial = privateMaterial;
        this.publicSpkiDer = publicSpkiDer;
    }

    public string PublicFingerprintSha256 => RsaSigningKeyMaterial.Fingerprint(publicSpkiDer);
    public string PublicPem => PemEncoding.WriteString("PUBLIC KEY", publicSpkiDer).Replace("\r\n", "\n").TrimEnd() + "\n";
    public string PrivatePem => PemEncoding.WriteString("PRIVATE KEY", privateMaterial.Bytes.Span).Replace("\r\n", "\n").TrimEnd() + "\n";

    public static GeneratedKeyHandoff Create(byte[] privatePkcs8, byte[] publicSpkiDer)
    {
        if (privatePkcs8 is not { Length: > 0 } || publicSpkiDer is not { Length: > 0 })
            throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);

        var derived = RsaSigningKeyMaterial.DerivePublic(privatePkcs8);
        if (!CryptographicOperations.FixedTimeEquals(derived, publicSpkiDer))
        {
            CryptographicOperations.ZeroMemory(derived);
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        }

        CryptographicOperations.ZeroMemory(derived);
        return new(SensitiveBytes.TakeOwnership(privatePkcs8), publicSpkiDer.ToArray());
    }

    public void Dispose()
    {
        privateMaterial.Dispose();
        CryptographicOperations.ZeroMemory(publicSpkiDer);
    }
}

internal sealed class SigningKeyGenerationHandoff(SigningKeyGenerationResult result,
    GeneratedKeyHandoff? handoff) : IDisposable
{
    public SigningKeyGenerationResult Result { get; } = result;
    public GeneratedKeyHandoff? Handoff { get; private set; } = handoff;

    public void Dispose()
    {
        Handoff?.Dispose();
        Handoff = null;
    }
}

internal interface ISigningKeyInitialHandoffService
{
    Task<SigningKeyGenerationHandoff> GenerateWithInitialHandoffAsync(string? bearer,
        GenerateSigningKeyRequest request, CancellationToken ct = default);
}

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
    Task<GeneratedKeyHandoff?> RecoverInitialHandoffAsync(KeyProtectionIdentity id, CancellationToken ct)
        => Task.FromResult<GeneratedKeyHandoff?>(null);
}
