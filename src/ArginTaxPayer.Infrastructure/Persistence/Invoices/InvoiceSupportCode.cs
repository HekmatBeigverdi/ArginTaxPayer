namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Reversible URL-safe base64 of all 128 bits of the existing random UUID.
/// No truncation, new sequence, schema change or collision domain is introduced.
/// </summary>
public static class InvoiceSupportCode
{
    public static string Encode(Guid id) =>
        Convert.ToBase64String(id.ToByteArray()).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string? code, out Guid id)
    {
        id = Guid.Empty;
        if (code is null || code.Length != 22 ||
            code.Any(c => !char.IsLetterOrDigit(c) && c is not ('-' or '_')))
            return false;
        try
        {
            var bytes = Convert.FromBase64String(code.Replace('-', '+').Replace('_', '/') + "==");
            if (bytes.Length != 16) return false;
            id = new Guid(bytes);
            return Encode(id) == code;
        }
        catch (FormatException) { return false; }
    }
}
