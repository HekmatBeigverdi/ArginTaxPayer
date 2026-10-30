using System.Security.Cryptography;
using System.Text;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Explicit one-invoice Sandbox operator consent. This object is only an
/// identity/consent guard: it is NOT a network or fiscal authorization.
/// Caller must separately verify authenticated actor, currently authorized
/// owner, trusted memory and certificate, finalized revision, validated rules,
/// durable signed outbox claim and expiry against server UTC.
/// </summary>
public sealed record SandboxSingleInvoiceConsent(
    Guid CustomerUserId, Guid TaxpayerId, Guid InvoiceId,
    Guid SandboxFiscalMemoryId, long InvoiceRevision,
    string ApprovedSnapshotSha256, DateTimeOffset ExpiresAtUtc,
    bool OperatorConfirmed, string Environment);

public static class SandboxSingleInvoiceConsentGuard
{
    public static bool Matches(
        SandboxSingleInvoiceConsent? consent,
        Guid authenticatedOwner, Guid taxpayerId, Guid invoiceId,
        Guid sandboxFiscalMemoryId, long finalizedRevision,
        string serverSnapshotSha256, DateTimeOffset serverNowUtc)
    {
        if (consent is null || !consent.OperatorConfirmed ||
            consent.Environment != "Sandbox" ||
            authenticatedOwner == Guid.Empty || taxpayerId == Guid.Empty ||
            invoiceId == Guid.Empty || sandboxFiscalMemoryId == Guid.Empty ||
            finalizedRevision < 1 || serverNowUtc.Offset != TimeSpan.Zero ||
            consent.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            consent.ExpiresAtUtc <= serverNowUtc ||
            consent.ExpiresAtUtc > serverNowUtc.AddMinutes(5) ||
            consent.CustomerUserId != authenticatedOwner ||
            consent.TaxpayerId != taxpayerId ||
            consent.InvoiceId != invoiceId ||
            consent.SandboxFiscalMemoryId != sandboxFiscalMemoryId ||
            consent.InvoiceRevision != finalizedRevision)
            return false;

        if (!TrySha256(consent.ApprovedSnapshotSha256, out var a) ||
            !TrySha256(serverSnapshotSha256, out var b))
            return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static bool TrySha256(string? hex, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (hex is not { Length: 64 }) return false;
        try { bytes = Convert.FromHexString(hex); return bytes.Length == 32; }
        catch (FormatException) { return false; }
    }
}
