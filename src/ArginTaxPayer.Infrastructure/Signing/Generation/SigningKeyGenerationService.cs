using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

internal sealed class SigningKeyGenerationService(ISigningGenerationJournal journal,
    IGeneratedKeyCustody custody, IGeneratedKeyFactory keys) : ISigningKeyGenerationService
{
    public async Task<SigningKeyGenerationResult> GenerateAsync(string? bearer, GenerateSigningKeyRequest request,
        CancellationToken ct = default)
    {
        if (request is null || request.TaxpayerId == Guid.Empty || request.FiscalMemoryId == Guid.Empty ||
            request.OperationId == Guid.Empty || !request.AcceptServerCustody ||
            request.ProfileId != SigningOperationPolicy.LegacyProfile ||
            request.ConsentVersion != SigningOperationPolicy.ConsentVersion)
            return new(SigningGenerationStatus.Invalid);
        var reserved = await journal.ReserveGenerationAsync(bearer, request, ct);
        if (reserved.Status is not (SigningGenerationStatus.Created or SigningGenerationStatus.Replayed))
            return new(reserved.Status);
        if (reserved.State == SigningOperationState.Committed)
            return await journal.ReadGeneratedAsync(bearer, reserved.Scope, Guid.Empty, reserved.OperationRecordId, ct);
        if (reserved.State == SigningOperationState.Failed) return new(SigningGenerationStatus.Conflict);
        try
        {
            await custody.EnsureReadyAsync(ct); // No claim or key generation if custody is unavailable.
            var admission = await journal.ClaimGenerationAsync(bearer, reserved, request.OperationId, ct);
            if (admission.Identity is not { } id ||
                admission.Status is not (SigningGenerationStatus.Created or SigningGenerationStatus.Replayed))
                return new(admission.Status);
            SealedGeneratedKey? receipt;
            if (admission.OwnsClaim)
            {
                // Never inside an EF retry delegate. Only the committed claim winner enters this branch.
                using var key = keys.Create();
                receipt = await custody.StageAsync(id, key, ct);
            }
            else receipt = await custody.RecoverAsync(id, ct);
            if (receipt is null) return new(SigningGenerationStatus.RecoveryRequired);
            if (receipt.Identity != id) return new(SigningGenerationStatus.IntegrityFailure);
            var result = await journal.CompleteGenerationAsync(bearer, request.OperationId, receipt, ct);
            return !admission.OwnsClaim && result.Status == SigningGenerationStatus.Created
                ? result with { Status = SigningGenerationStatus.Replayed } : result;
        }
        catch (KeyProtectionException error)
        {
            return new(error.Failure == KeyProtectionFailure.Unavailable
                ? SigningGenerationStatus.Unavailable : SigningGenerationStatus.RecoveryRequired);
        }
        catch (OperationCanceledException) { return new(SigningGenerationStatus.RecoveryRequired); }
        catch (CryptographicException) { return new(SigningGenerationStatus.RecoveryRequired); }
    }

    public Task<SigningKeyGenerationResult> ReadPublicAsync(string? bearer, Guid taxpayerId,
        Guid fiscalMemoryId, Guid keyVersionId, CancellationToken ct = default) =>
        journal.ReadPublicAsync(bearer, taxpayerId, fiscalMemoryId, keyVersionId, ct);
}
