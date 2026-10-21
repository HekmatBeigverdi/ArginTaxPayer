using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;

internal sealed record TrustedInvoiceSnapshot(
    Guid InvoiceId, Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId,
    Guid KeyVersionId, long InvoiceRevision, long KeyRevision,
    string SnapshotJson, string SnapshotSha256, string ProfileId, byte[] PublicSpkiDer);

internal enum SigningAdmissionStatus { Allowed, Forbidden, Conflict, Unavailable, IntegrityFailure }
internal sealed record SigningAdmission(SigningAdmissionStatus Status, Guid IntentId);

internal interface ITrustedInvoiceSnapshotSource
{
    Task<TrustedInvoiceSnapshot?> LoadAsync(string? bearer, AuthorizedSigningRequest request,
        CancellationToken ct);
}

internal interface IInvoiceSigningAudit
{
    Task<SigningAdmission> BeginAsync(string? bearer, TrustedInvoiceSnapshot snapshot, CancellationToken ct);
    Task<DigitalSigningStatus> CompleteAsync(string? bearer, Guid intentId, TrustedInvoiceSnapshot snapshot,
        string signatureBase64, CancellationToken ct);
}

internal interface IInvoiceSigningCustody
{
    Task<SensitiveBytes> UnwrapAsync(TrustedInvoiceSnapshot snapshot, CancellationToken ct);
}
