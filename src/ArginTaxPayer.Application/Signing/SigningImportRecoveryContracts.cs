namespace ArginTaxPayer.Application.Signing;

/// <summary>Owner-only reconciliation without retransmitting a private key.</summary>
public interface ISigningImportRecovery
{
    Task<SigningGenerationStatus> RecoverAsync(string? bearer, Guid taxpayerId,
        Guid fiscalMemoryId, CancellationToken ct = default);
}
