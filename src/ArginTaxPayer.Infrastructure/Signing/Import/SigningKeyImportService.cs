using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Import;

internal sealed class SigningKeyImportService(ISigningImportJournal journal, IGeneratedKeyCustody custody,
    Pkcs8SigningKeyParser parser) : ISigningKeyImportService
{
    public async Task<SigningKeyGenerationResult> ImportAsync(string? bearer, ImportSigningKeyRequest request,
        SensitiveKeyInput input, CancellationToken ct = default)
    {
        using (input)
        {
            if (input is null || request is null || request.TaxpayerId == Guid.Empty ||
                request.FiscalMemoryId == Guid.Empty || request.OperationId == Guid.Empty ||
                !request.AcceptServerCustody || request.ProfileId != SigningOperationPolicy.LegacyProfile ||
                request.ConsentVersion != SigningOperationPolicy.ConsentVersion)
                return new(SigningGenerationStatus.Invalid);
            try
            {
                var authorization = await journal.AuthorizeImportAsync(bearer, request, ct);
                if (authorization.Status != SigningGenerationStatus.Replayed) return new(authorization.Status);
                ct.ThrowIfCancellationRequested();
                using var key = parser.Parse(input);
                ct.ThrowIfCancellationRequested();
                var reservation = await journal.ReserveImportAsync(bearer, request, key.PublicFingerprintSha256, ct);
                if (reservation.Status is not (SigningGenerationStatus.Created or SigningGenerationStatus.Replayed))
                    return new(reservation.Status);
                if (reservation.State == SigningOperationState.Committed)
                    return await journal.ReadImportAsync(bearer, reservation, ct);
                if (reservation.State == SigningOperationState.Failed) return new(SigningGenerationStatus.Conflict);
                await custody.EnsureReadyAsync(ct);
                var admission = await journal.ClaimImportAsync(bearer, reservation, request.OperationId,
                    key.PublicFingerprintSha256, ct);
                if (admission.Identity is not { } id ||
                    admission.Status is not (SigningGenerationStatus.Created or SigningGenerationStatus.Replayed))
                    return new(admission.Status);
                var receipt = admission.OwnsClaim ? await custody.StageAsync(id, key, ct) : await custody.RecoverAsync(id, ct);
                if (receipt is null) return new(SigningGenerationStatus.RecoveryRequired);
                if (receipt.Identity != id || receipt.PublicFingerprintSha256 != key.PublicFingerprintSha256)
                    return new(SigningGenerationStatus.IntegrityFailure);
                var result = await journal.CompleteImportAsync(bearer, request.OperationId, receipt, ct);
                return !admission.OwnsClaim && result.Status == SigningGenerationStatus.Created
                    ? result with { Status = SigningGenerationStatus.Replayed } : result;
            }
            catch (KeyImportException) { return new(SigningGenerationStatus.Invalid); }
            catch (KeyProtectionException error)
            {
                return new(error.Failure == KeyProtectionFailure.Unavailable
                    ? SigningGenerationStatus.Unavailable : SigningGenerationStatus.RecoveryRequired);
            }
            catch (OperationCanceledException) { return new(SigningGenerationStatus.RecoveryRequired); }
        }
    }
}
