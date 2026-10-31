namespace ArginTaxPayer.Application.Invoices;

/// <summary>
/// Inquiry-only retry decision. A retry is permission to make a new read-only
/// authority inquiry; it NEVER authorizes resubmitting the invoice packet.
/// Immutable inputs allow a queue adapter to persist its own attempt counters.
/// </summary>
public static class Phase11InquiryRetryPolicy
{
    public const int MaximumAttempts = 6;
    public static readonly TimeSpan MaximumDelay = TimeSpan.FromMinutes(30);

    public static Phase11InquiryRetryDecision Decide(
        string? safeCode, int completedAttempts, DateTime lastAttemptUtc)
    {
        if (completedAttempts < 0 || lastAttemptUtc.Kind != DateTimeKind.Utc)
            return new(false, null, "inquiry_retry_invalid_input");
        if (completedAttempts >= MaximumAttempts)
            return new(false, null, "inquiry_retry_exhausted");
        if (!IsTransient(safeCode))
            return new(false, null, "inquiry_retry_not_transient");

        // Deterministic exponential backoff, no randomized ordering between
        // multiple hosts; a durable lease is required before execution.
        var minutes = Math.Min(1 << Math.Min(completedAttempts, 5), 30);
        return new(true, lastAttemptUtc.AddMinutes(minutes),
            "inquiry_retry_eligible");
    }

    public static bool IsTransient(string? safeCode) => safeCode is
        "inquiry_timeout" or
        "inquiry_network_unavailable" or
        "inquiry_http_429" or
        "inquiry_http_502" or
        "inquiry_http_503" or
        "inquiry_http_504";

    public static bool MayResendInvoice => false;
}

public sealed record Phase11InquiryRetryDecision(
    bool MayRetryInquiry, DateTime? NextInquiryUtc, string SafeCode);
