using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Offline;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;

internal sealed class InvoiceSnapshotSigningService(
    ITrustedInvoiceSnapshotSource source,
    IInvoiceSigningAudit audit,
    IInvoiceSigningCustody custody,
    ILegacyOfflineSigningEngine legacySigner) : IDigitalSigningService
{
    public async Task<SigningResult> SignAsync(string? bearer, AuthorizedSigningRequest request,
        CancellationToken ct = default)
    {
        if (!ValidRequest(request)) return new(DigitalSigningStatus.Invalid);
        try
        {
            var snapshot = await source.LoadAsync(bearer, request, ct);
            if (snapshot is null) return new(DigitalSigningStatus.Forbidden);
            if (!MatchesRequest(snapshot, request) || !ValidSnapshot(snapshot))
                return new(DigitalSigningStatus.IntegrityFailure);

            // Durable intent must exist before custody access or private material is unwrapped.
            var admission = await audit.BeginAsync(bearer, snapshot, ct);
            if (admission.Status != SigningAdmissionStatus.Allowed)
                return new(Map(admission.Status));

            using var privateMaterial = await custody.UnwrapAsync(snapshot, ct);
            var signature = legacySigner.Sign(privateMaterial, snapshot.PublicSpkiDer,
                Encoding.UTF8.GetBytes(snapshot.SnapshotJson), LegacySigningMode.ObjectV030,
                LegacySigningPurpose.Content).SignatureBase64;
            var completed = await audit.CompleteAsync(bearer, admission.IntentId, snapshot, signature, ct);
            return completed == DigitalSigningStatus.Signed
                ? new(completed, signature, snapshot.SnapshotSha256, snapshot.KeyVersionId, snapshot.ProfileId)
                : new(completed);
        }
        catch (KeyProtectionException error)
        {
            return new(error.Failure switch
            {
                KeyProtectionFailure.Unavailable => DigitalSigningStatus.Unavailable,
                KeyProtectionFailure.Integrity => DigitalSigningStatus.IntegrityFailure,
                _ => DigitalSigningStatus.RecoveryRequired
            });
        }
        catch (OperationCanceledException) { return new(DigitalSigningStatus.RecoveryRequired); }
        catch (CryptographicException) { return new(DigitalSigningStatus.IntegrityFailure); }
    }

    private static bool ValidRequest(AuthorizedSigningRequest? request) => request is not null &&
        request.InvoiceId != Guid.Empty && request.CustomerUserId != Guid.Empty &&
        request.TaxpayerId != Guid.Empty && request.FiscalMemoryId != Guid.Empty &&
        request.KeyVersionId != Guid.Empty && request.ExpectedInvoiceRevision > 0 &&
        request.ExpectedKeyRevision > 0 && SigningOperationPolicy.ValidFingerprint(request.SnapshotSha256) &&
        request.ProfileId == SigningOperationPolicy.LegacyProfile;

    private static bool MatchesRequest(TrustedInvoiceSnapshot s, AuthorizedSigningRequest r) =>
        s.InvoiceId == r.InvoiceId && s.CustomerUserId == r.CustomerUserId && s.TaxpayerId == r.TaxpayerId &&
        s.FiscalMemoryId == r.FiscalMemoryId && s.KeyVersionId == r.KeyVersionId &&
        s.InvoiceRevision == r.ExpectedInvoiceRevision && s.KeyRevision == r.ExpectedKeyRevision &&
        s.SnapshotSha256 == r.SnapshotSha256 && s.ProfileId == r.ProfileId;

    private static bool ValidSnapshot(TrustedInvoiceSnapshot s)
    {
        if (string.IsNullOrWhiteSpace(s.SnapshotJson) || s.SnapshotJson.Length > 8 * 1024 * 1024 ||
            s.SnapshotJson.Any(char.IsControl)) return false;
        try
        {
            using var doc = JsonDocument.Parse(s.SnapshotJson);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s.SnapshotJson))).ToLowerInvariant();
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(s.SnapshotSha256));
        }
        catch (JsonException) { return false; }
    }

    private static DigitalSigningStatus Map(SigningAdmissionStatus status) => status switch
    {
        SigningAdmissionStatus.Forbidden => DigitalSigningStatus.Forbidden,
        SigningAdmissionStatus.Conflict => DigitalSigningStatus.Conflict,
        SigningAdmissionStatus.Unavailable => DigitalSigningStatus.Unavailable,
        SigningAdmissionStatus.IntegrityFailure => DigitalSigningStatus.IntegrityFailure,
        _ => DigitalSigningStatus.RecoveryRequired
    };
}
