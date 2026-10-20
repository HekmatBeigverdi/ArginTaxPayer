using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>PostgreSQL built-in JSONB path extraction for scoped invoice listing.</summary>
public static class InvoiceJsonFields
{
    [DbFunction("jsonb_extract_path_text", IsBuiltIn = true)]
    public static string? Text(string jsonb, string key) =>
        throw new NotSupportedException("Only available inside translated EF SQL.");
}
