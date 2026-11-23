namespace ArginTaxPayer.Domain.Invoices;

/// <summary>Standard submission window measured from the actual issue instant.</summary>
public static class InvoiceSubmissionDeadline
{
    private static readonly TimeSpan MaximumAge = TimeSpan.FromDays(12);

    public static bool HasElapsed(DateTime issueDateUtc, DateTime nowUtc) =>
        nowUtc - issueDateUtc > MaximumAge;
}
