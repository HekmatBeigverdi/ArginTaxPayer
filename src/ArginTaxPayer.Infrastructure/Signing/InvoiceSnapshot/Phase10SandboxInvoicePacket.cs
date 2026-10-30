using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;

/// <summary>
/// Immutable, in-memory Sandbox packet metadata. The private signing key is
/// deliberately not part of this type; callers must obtain it ephemerally from
/// protected custody and dispose it after <see cref="Build"/> returns.
/// </summary>
public sealed record Phase10SandboxInvoicePacket(
    Guid InvoiceId,
    Guid CustomerUserId,
    Guid TaxpayerId,
    Guid SandboxFiscalMemoryId,
    long InvoiceRevision,
    string SnapshotSha256,
    string FiscalTaxId,
    Guid RequestTraceId,
    string SigningKeyFingerprintSha256,
    string AuthorityEncryptionKeyId,
    string Jws,
    string Jwe,
    string JwsSha256,
    string JweSha256);

public static class Phase10SandboxInvoicePacketBuilder
{
    public static Phase10SandboxInvoicePacket Build(
        Guid invoiceId,
        Guid customerUserId,
        Guid taxpayerId,
        Guid sandboxFiscalMemoryId,
        long invoiceRevision,
        string snapshotSha256,
        string fiscalTaxId,
        Guid requestTraceId,
        string signingKeyFingerprintSha256,
        string authorityEncryptionKeyId,
        string invoiceJson,
        RSA signingKeyFromProtectedCustody,
        X509Certificate2 signingCertificate,
        RSA authorityEncryptionPublicKey,
        DateTime signedAtUtc)
    {
        if (invoiceId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || sandboxFiscalMemoryId == Guid.Empty ||
            requestTraceId == Guid.Empty || invoiceRevision < 1 ||
            !Sha256(snapshotSha256) || !Sha256(signingKeyFingerprintSha256) ||
            fiscalTaxId is not { Length: 22 } ||
            !fiscalTaxId.All(c => char.IsAsciiLetterOrDigit(c)) ||
            authorityEncryptionKeyId is not { Length: > 0 and <= 128 } ||
            authorityEncryptionKeyId.Any(char.IsControl))
            throw new ArgumentException("Invalid Sandbox packet identity.");

        ArgumentNullException.ThrowIfNull(signingKeyFromProtectedCustody);
        ArgumentNullException.ThrowIfNull(signingCertificate);
        ArgumentNullException.ThrowIfNull(authorityEncryptionPublicKey);
        if (invoiceJson is not { Length: > 2 and <= 1_048_576 })
            throw new ArgumentException("Invalid invoice JSON.");

        using var document = JsonDocument.Parse(invoiceJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Invoice payload must be an object.");
        if (!ContainsFiscalTaxId(document.RootElement, fiscalTaxId))
            throw new CryptographicException("Fiscal identifier is not bound to invoice payload.");

        var signingSpki = signingKeyFromProtectedCustody.ExportSubjectPublicKeyInfo();
        using var certificateKey = signingCertificate.GetRSAPublicKey();
        if (certificateKey is null ||
            !certificateKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(signingSpki))
            throw new CryptographicException("Signing certificate is not bound to protected key.");
        var actualFingerprint = Convert.ToHexString(SHA256.HashData(signingSpki)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualFingerprint),
                Encoding.ASCII.GetBytes(signingKeyFingerprintSha256.ToLowerInvariant())))
            throw new CryptographicException("Signing key fingerprint is not bound to protected key.");

        var jws = Phase10SandboxInvoicePacketCryptography.SignJws(
            invoiceJson, signingKeyFromProtectedCustody, signingCertificate, signedAtUtc);
        var jwe = Phase10SandboxInvoicePacketCryptography.EncryptJwe(
            jws, authorityEncryptionPublicKey, authorityEncryptionKeyId);
        return new(
            invoiceId, customerUserId, taxpayerId, sandboxFiscalMemoryId,
            invoiceRevision, snapshotSha256.ToLowerInvariant(), fiscalTaxId,
            requestTraceId, signingKeyFingerprintSha256.ToLowerInvariant(),
            authorityEncryptionKeyId, jws, jwe,
            Hash(jws), Hash(jwe));
    }

    private static bool ContainsFiscalTaxId(JsonElement root, string fiscalTaxId)
    {
        if (root.ValueKind == JsonValueKind.String)
            return string.Equals(root.GetString(), fiscalTaxId, StringComparison.Ordinal);
        if (root.ValueKind == JsonValueKind.Object)
            return root.EnumerateObject().Any(x => ContainsFiscalTaxId(x.Value, fiscalTaxId));
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Any(x => ContainsFiscalTaxId(x, fiscalTaxId));
        return false;
    }

    private static bool Sha256(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or
            >= 'A' and <= 'F');

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
