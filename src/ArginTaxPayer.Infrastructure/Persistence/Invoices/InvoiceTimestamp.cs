namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>PostgreSQL timestamps preserve microseconds; .NET timestamps can contain finer ticks.</summary>
internal static class InvoiceTimestamp
{
    internal static DateTime TruncateToMicroseconds(DateTime value)
    {
        if(value.Kind!=DateTimeKind.Utc)
            throw new ArgumentException("Invoice timestamp must be UTC.",nameof(value));
        return new DateTime(value.Ticks-value.Ticks%TimeSpan.TicksPerMicrosecond,DateTimeKind.Utc);
    }
}
