using System.Security.Cryptography;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Server-owned admission context for the one-invoice Sandbox submission path.
/// It is a pure gate: it performs no persistence, signing or network I/O.
/// </summary>
public sealed record SandboxSubmissionAdmissionContext(
    Guid CustomerUserId,
    Guid AuthenticatedActorId,
    Guid TaxpayerId,
    Guid InvoiceId,
    Guid SandboxFiscalMemoryId,
    long InvoiceRevision,
    InvoiceLifecycleState InvoiceState,
    InvoiceReviewState ReviewState,
    string ApprovedSnapshotSha256,
    string ServerSnapshotSha256,
    string Environment,
    SandboxSingleInvoiceConsent? Consent,
    DateTimeOffset ServerNowUtc);

public sealed record SandboxSubmissionAdmissionResult(
    bool IsAdmitted, IReadOnlyList<string> BlockingCodes)
{
    public static SandboxSubmissionAdmissionResult Denied(params string[] codes) =>
        new(false, Array.AsReadOnly(codes.Distinct(StringComparer.Ordinal).ToArray()));
}

public static class SandboxSubmissionAdmissionPolicy
{
    public static SandboxSubmissionAdmissionResult Evaluate(
        SandboxSubmissionAdmissionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var blockers = new List<string>();

        if (context.CustomerUserId == Guid.Empty ||
            context.AuthenticatedActorId != context.CustomerUserId)
            blockers.Add("owner.mismatch");
        if (context.TaxpayerId == Guid.Empty || context.InvoiceId == Guid.Empty ||
            context.SandboxFiscalMemoryId == Guid.Empty)
            blockers.Add("scope.invalid");
        if (context.InvoiceRevision < 1 || context.InvoiceState != InvoiceLifecycleState.Finalized)
            blockers.Add("invoice.not_finalized");
        if (context.ReviewState != InvoiceReviewState.Approved)
            blockers.Add("review.not_approved");
        if (!string.Equals(context.Environment, "Sandbox", StringComparison.Ordinal))
            blockers.Add("environment.not_sandbox");
        if (!ValidSha256(context.ApprovedSnapshotSha256) ||
            !ValidSha256(context.ServerSnapshotSha256))
            blockers.Add("snapshot_hash.invalid");
        else if (!CryptographicOperations.FixedTimeEquals(
                     Convert.FromHexString(context.ApprovedSnapshotSha256),
                     Convert.FromHexString(context.ServerSnapshotSha256)))
            blockers.Add("snapshot_hash.mismatch");
        if (context.ServerNowUtc.Offset != TimeSpan.Zero)
            blockers.Add("clock.not_utc");
        if (context.Consent is not null &&
            context.Consent.InvoiceRevision != context.InvoiceRevision)
            blockers.Add("revision.mismatch");
        if (context.Consent is not null &&
            !string.Equals(context.Consent.ApprovedSnapshotSha256,
                context.ServerSnapshotSha256, StringComparison.OrdinalIgnoreCase))
            blockers.Add("consent.snapshot_hash.mismatch");
        if (context.Consent is null ||
            !SandboxSingleInvoiceConsentGuard.Matches(
                context.Consent, context.AuthenticatedActorId, context.TaxpayerId,
                context.InvoiceId, context.SandboxFiscalMemoryId,
                context.InvoiceRevision, context.ServerSnapshotSha256,
                context.ServerNowUtc))
            blockers.Add("consent.invalid_or_expired");

        return blockers.Count == 0
            ? new(true, Array.Empty<string>())
            : SandboxSubmissionAdmissionResult.Denied(blockers.ToArray());
    }

    private static bool ValidSha256(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or
            >= 'A' and <= 'F');
}
