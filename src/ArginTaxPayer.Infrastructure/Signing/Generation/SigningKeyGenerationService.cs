using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

internal sealed class SigningKeyGenerationService(ISigningGenerationJournal journal,
    IGeneratedKeyCustody custody, IGeneratedKeyFactory keys) : ISigningKeyGenerationService,
    ISigningKeyInitialHandoffService
{
    public async Task<SigningKeyGenerationResult> GenerateAsync(string? bearer, GenerateSigningKeyRequest request,
        CancellationToken ct = default)
        => (await GenerateCoreAsync(bearer, request, false, ct)).Result;

    public Task<SigningKeyGenerationHandoff> GenerateWithInitialHandoffAsync(string? bearer,
        GenerateSigningKeyRequest request, CancellationToken ct = default) =>
        GenerateCoreAsync(bearer, request, true, ct);

    private async Task<SigningKeyGenerationHandoff> GenerateCoreAsync(string? bearer,
        GenerateSigningKeyRequest request, bool includeInitialHandoff, CancellationToken ct)
    {
        if (request is null || request.TaxpayerId == Guid.Empty || request.FiscalMemoryId == Guid.Empty ||
            request.OperationId == Guid.Empty || !request.AcceptServerCustody ||
            request.ProfileId != SigningOperationPolicy.LegacyProfile ||
            request.ConsentVersion != SigningOperationPolicy.ConsentVersion)
            return new(new(SigningGenerationStatus.Invalid), null);
        var reserved = await journal.ReserveGenerationAsync(bearer, request, ct);
        if (reserved.Status is not (SigningGenerationStatus.Created or SigningGenerationStatus.Replayed))
            return new(new(reserved.Status), null);
        if (reserved.State == SigningOperationState.Committed)
            return new(await journal.ReadGeneratedAsync(bearer, reserved.Scope, Guid.Empty, reserved.OperationRecordId, ct), null);
        if (reserved.State == SigningOperationState.Failed)
            return new(new(SigningGenerationStatus.Conflict), null);
        GeneratedKeyHandoff? handoff = null;
        try
        {
            await custody.EnsureReadyAsync(ct); // No claim or key generation if custody is unavailable.
            var admission = await journal.ClaimGenerationAsync(bearer, reserved, request.OperationId, ct);
            if (admission.Identity is not { } id ||
                admission.Status is not (SigningGenerationStatus.Created or SigningGenerationStatus.Replayed))
                return new(new(admission.Status), null);
            SealedGeneratedKey? receipt;
            if (admission.OwnsClaim)
            {
                // Never inside an EF retry delegate. Only the committed claim winner enters this branch.
                using var key = keys.Create();
                if (includeInitialHandoff)
                    handoff = GeneratedKeyHandoff.Create(key.PrivateMaterial.Bytes.ToArray(), key.PublicSpkiDer);
                receipt = await custody.StageAsync(id, key, ct);
            }
            else
            {
                receipt = await custody.RecoverAsync(id, ct);
                if (includeInitialHandoff && receipt is not null)
                    handoff = await custody.RecoverInitialHandoffAsync(id, ct);
            }
            if (receipt is null) return new(new(SigningGenerationStatus.RecoveryRequired), null);
            if (receipt.Identity != id) return new(new(SigningGenerationStatus.IntegrityFailure), null);
            var result = await journal.CompleteGenerationAsync(bearer, request.OperationId, receipt, ct);
            var effective = !admission.OwnsClaim && handoff is null && result.Status == SigningGenerationStatus.Created
                ? result with { Status = SigningGenerationStatus.Replayed } : result;
            // Only the transaction that first commits may release an initial handoff.
            // Concurrent recovery and committed-operation replay never release it again.
            if (result.Status != SigningGenerationStatus.Created)
            {
                handoff?.Dispose();
                handoff = null;
            }
            return new(effective, handoff);
        }
        catch (KeyProtectionException error)
        {
            handoff?.Dispose();
            return new(new(error.Failure == KeyProtectionFailure.Unavailable
                ? SigningGenerationStatus.Unavailable : SigningGenerationStatus.RecoveryRequired), null);
        }
        catch (OperationCanceledException)
        {
            handoff?.Dispose();
            return new(new(SigningGenerationStatus.RecoveryRequired), null);
        }
        catch (CryptographicException)
        {
            handoff?.Dispose();
            return new(new(SigningGenerationStatus.RecoveryRequired), null);
        }
    }

    public Task<SigningKeyGenerationResult> ReadPublicAsync(string? bearer, Guid taxpayerId,
        Guid fiscalMemoryId, Guid keyVersionId, CancellationToken ct = default) =>
        journal.ReadPublicAsync(bearer, taxpayerId, fiscalMemoryId, keyVersionId, ct);
}
