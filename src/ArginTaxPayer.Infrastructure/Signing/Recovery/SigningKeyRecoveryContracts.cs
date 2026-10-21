using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Recovery;

internal enum SigningRecoveryStatus { Completed, Paused, MissingKeyProtection, Conflict, Invalid }

internal sealed record SigningRecoveryItem(KeyProtectionIdentity Identity, int ExpectedEnvelopeVersion);
internal sealed record SigningRecoveryCheckpoint(int NextIndex, int Completed, string? LastPath);
internal sealed record SigningRecoveryResult(SigningRecoveryStatus Status, SigningRecoveryCheckpoint Checkpoint);

internal interface ISigningKeyRecoveryService
{
    Task<SigningRecoveryResult> RewrapAsync(IReadOnlyList<SigningRecoveryItem> items,
        SigningRecoveryCheckpoint checkpoint, CancellationToken ct = default);
}
