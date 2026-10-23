using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Phase10 Step09 source-verified binding for a Phase09 signing result.
/// Importantly Phase09's signer currently signs its own persisted SnapshotJson,
/// which might NOT equal the authority wire bytes. This binding can report
/// metadata agreement but cannot certify an authority-wire signature.
/// </summary>
public enum InvoiceWireSignatureBindingStatus
{
    MetadataMatchedButWireUnverified = 1,
    SigningFailed = 2,
    ScopeMismatch = 3,
    HashMismatch = 4,
    KeyVersionMismatch = 5,
    UnsupportedProfile = 6,
    InvalidSignatureEnvelope = 7
}

public sealed record InvoiceWireSigningIntent(
    Guid KeyVersionId, long KeyRevision, string ProfileId);

public sealed record InvoiceWireSignatureBindingResult(
    InvoiceWireSignatureBindingStatus Status, string SafeCode,
    string? WireSha256 = null)
{
    // A Phase09 SnapshotJson signature is not proof of signing exact invoice
    // packet bytes; separate authorized offline verification is mandatory.
    public bool IsWireSignatureVerified => false;
    public bool IsFiscalSubmissionAuthorized => false;
}

public static class Phase10WireSignatureBinding
{
    public static InvoiceWireSignatureBindingResult Check(
        InvoiceDispatchPreparedSnapshot prepared,
        InvoiceWireSigningIntent intent,
        AuthorizedSigningRequest request,
        SigningResult result)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        static InvoiceWireSignatureBindingResult Fail(InvoiceWireSignatureBindingStatus status,
            string code) => new(status, code);

        if (prepared.InvoiceId != request.InvoiceId ||
            prepared.CustomerUserId != request.CustomerUserId ||
            prepared.TaxpayerId != request.TaxpayerId ||
            prepared.FiscalMemoryId != request.FiscalMemoryId ||
            prepared.InvoiceRevision != request.ExpectedInvoiceRevision)
            return Fail(InvoiceWireSignatureBindingStatus.ScopeMismatch, "invoice.signing.scope_mismatch");

        if (intent.KeyVersionId == Guid.Empty || intent.KeyRevision < 1 ||
            intent.KeyVersionId != request.KeyVersionId ||
            request.ExpectedKeyRevision != intent.KeyRevision ||
            result.KeyVersionId != intent.KeyVersionId)
            return Fail(InvoiceWireSignatureBindingStatus.KeyVersionMismatch, "invoice.signing.key_mismatch");

        if (string.IsNullOrWhiteSpace(intent.ProfileId) ||
            intent.ProfileId != request.ProfileId ||
            result.ProfileId != intent.ProfileId)
            return Fail(InvoiceWireSignatureBindingStatus.UnsupportedProfile, "invoice.signing.profile_mismatch");

        var actualWire = Convert.ToHexString(
            SHA256.HashData(prepared.CopyWireBytes())).ToLowerInvariant();
        if (!IsMatchingHash(prepared.WirePayloadSha256, actualWire) ||
            !IsMatchingHash(request.SnapshotSha256, actualWire) ||
            !IsMatchingHash(result.SnapshotSha256, actualWire))
            return Fail(InvoiceWireSignatureBindingStatus.HashMismatch, "invoice.signing.wire_hash_mismatch");

        if (result.Status != DigitalSigningStatus.Signed)
            return Fail(InvoiceWireSignatureBindingStatus.SigningFailed, "invoice.signing.failed");

        // Format-only validation. This does NOT verify the cryptographic signature.
        if (string.IsNullOrWhiteSpace(result.SignatureBase64) ||
            result.SignatureBase64.Length > 8192)
            return Fail(InvoiceWireSignatureBindingStatus.InvalidSignatureEnvelope,
                "invoice.signing.invalid_envelope");
        try
        {
            var sig = Convert.FromBase64String(result.SignatureBase64);
            if (sig.Length is < 128 or > 1024)
                return Fail(InvoiceWireSignatureBindingStatus.InvalidSignatureEnvelope,
                    "invoice.signing.invalid_envelope");
        }
        catch (FormatException)
        {
            return Fail(InvoiceWireSignatureBindingStatus.InvalidSignatureEnvelope,
                "invoice.signing.invalid_envelope");
        }

        // Never transition DispatchState to Signed based on this alone.
        // Exact wire-byte cryptographic verification with the selected SPKI
        // and authenticated signing intent is a mandatory next integration gate.
        return new(InvoiceWireSignatureBindingStatus.MetadataMatchedButWireUnverified,
            "invoice.signing.wire_cryptographic_verification_pending", actualWire);
    }

    private static bool IsMatchingHash(string? a, string expected) =>
        a is { Length: 64 } &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(a.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(expected));
}
