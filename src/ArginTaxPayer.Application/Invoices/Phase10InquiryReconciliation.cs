using System.Text.Json;

namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Phase10 Step14. A safe, read-only model for one authenticated, scoped inquiry.
/// The official endpoint and its profile must be verified before a live adapter
/// is activated. A missing record can never prove a packet was not delivered.
/// </summary>
public sealed record InvoiceReconciliationIdentity(
    Guid OperationId, Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId,
    string PacketUid, string PacketType, string FiscalIdentifier,
    string? AuthorityReference);

public enum InvoiceReconciliationOutcome
{
    CorrelatedPendingReview = 1,
    NoConclusiveResult = 2,
    ConflictingEvidence = 3
}

public sealed record InvoiceReconciliationDecision(
    InvoiceReconciliationOutcome Outcome, string SafeCode,
    string? MatchedReference = null)
{
    public bool IsFiscalAccepted => false;
    public bool ProvesNotDelivered => false;
    public bool MayAutomaticallyResend => false;
    public bool IsFiscalSubmissionAuthorized => false;
}

/// <summary>
/// Archived SDK 2.0.32 InquiryResultDTO shape: array of results containing
/// uid, packetType, fiscalId, referenceNumber, status, data and sign.
/// Correlation only. No status-string heuristics or unsigned proof acceptance.
/// </summary>
public static class Phase10InquiryReconciliation
{
    public static InvoiceReconciliationDecision Evaluate(
        InvoiceReconciliationIdentity expected, string? providerJson)
    {
        ArgumentNullException.ThrowIfNull(expected);
        static InvoiceReconciliationDecision Unknown() =>
            new(InvoiceReconciliationOutcome.NoConclusiveResult,
                "invoice.reconciliation.inconclusive");
        static InvoiceReconciliationDecision Conflict() =>
            new(InvoiceReconciliationOutcome.ConflictingEvidence,
                "invoice.reconciliation.identity_conflict");
        if (expected.OperationId == Guid.Empty ||
            expected.CustomerUserId == Guid.Empty ||
            expected.TaxpayerId == Guid.Empty ||
            expected.FiscalMemoryId == Guid.Empty ||
            !Opaque(expected.PacketUid, 128) ||
            !Opaque(expected.PacketType, 80) ||
            !Opaque(expected.FiscalIdentifier, 64) ||
            (expected.AuthorityReference is not null &&
             !Opaque(expected.AuthorityReference, 128)) ||
            string.IsNullOrWhiteSpace(providerJson) ||
            providerJson.Length > 262144)
            return Unknown();
        try
        {
            using var parsed = JsonDocument.Parse(providerJson,
                new JsonDocumentOptions { MaxDepth = 16, AllowTrailingCommas = false });
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Array) return Unknown();
            if (root.GetArrayLength() == 0) return Unknown(); // not proof of non-delivery
            if (root.GetArrayLength() != 1) return Conflict(); // no auto-select among matches
            var item = root[0];
            if (item.ValueKind != JsonValueKind.Object ||
                HasDuplicateProperties(item) ||
                !ReadString(item, "uid", out var uid) ||
                !ReadString(item, "packetType", out var type) ||
                !ReadString(item, "fiscalId", out var fiscalId) ||
                !ReadString(item, "referenceNumber", out var reference) ||
                !ReadString(item, "status", out var status) ||
                !Opaque(uid, 128) || !Opaque(type, 80) ||
                !Opaque(fiscalId, 64) || !Opaque(reference, 128) ||
                string.IsNullOrWhiteSpace(status) || status.Length > 80)
                return Unknown();

            if (!string.Equals(uid, expected.PacketUid, StringComparison.Ordinal) ||
                !string.Equals(type, expected.PacketType, StringComparison.Ordinal) ||
                !string.Equals(fiscalId, expected.FiscalIdentifier, StringComparison.Ordinal) ||
                (expected.AuthorityReference is not null &&
                 !string.Equals(reference, expected.AuthorityReference, StringComparison.Ordinal)))
                return Conflict();

            // Status may say SUCCESS/FAILED/PENDING in different archived SDKs.
            // None can grant a legal final state without verified official
            // semantics, trusted authority inquiry, and persisted evidence.
            return new(InvoiceReconciliationOutcome.CorrelatedPendingReview,
                "invoice.reconciliation.correlated_pending_review", reference);
        }
        catch (JsonException) { return Unknown(); }
        catch (InvalidOperationException) { return Unknown(); }
    }

    private static bool ReadString(JsonElement element, string property, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var found) ||
            found.ValueKind != JsonValueKind.String)
            return false;
        value = found.GetString();
        return true;
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            if (!seen.Add(property.Name)) return true;
        return false;
    }

    private static bool Opaque(string? text, int max) =>
        text is { Length: > 0 } && text.Length <= max &&
        text.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
            >= '0' and <= '9' or '_' or '-');
}

/// <summary>
/// An official API adapter must implement this with authorization, trusted
/// environment binding, TLS, request limits, and protected read-only credentials.
/// No production implementation or DI registration exists at Step14.
/// </summary>
public interface IInvoiceAuthorityInquiry
{
    Task<InvoiceReconciliationDecision> InquireAsync(
        InvoiceReconciliationIdentity expected, CancellationToken ct = default);
}

/// <summary>Default unavailable boundary; does not do network I/O.</summary>
public sealed class DisabledInvoiceAuthorityInquiry : IInvoiceAuthorityInquiry
{
    public Task<InvoiceReconciliationDecision> InquireAsync(
        InvoiceReconciliationIdentity expected, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(expected);
        return Task.FromResult(new InvoiceReconciliationDecision(
            InvoiceReconciliationOutcome.NoConclusiveResult,
            "invoice.reconciliation.inquiry_not_configured"));
    }
}
