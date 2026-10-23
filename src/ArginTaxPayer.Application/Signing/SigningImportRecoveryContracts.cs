namespace ArginTaxPayer.Application.Signing;

/// <summary>Owner-only reconciliation without retransmitting a private key.</summary>
public sealed record SigningImportRecoveryResult(SigningGenerationStatus Status, string SafeStage);

public interface ISigningImportRecovery
{
    Task<SigningImportRecoveryResult> RecoverAsync(string? bearer, Guid taxpayerId,
        Guid fiscalMemoryId, CancellationToken ct = default);
}
