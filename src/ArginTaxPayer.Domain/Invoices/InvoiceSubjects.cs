namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Invoice header 'ins' (historical technical instruction: 1 original,
/// 2 correction, 3 cancellation, 4 sales return).
/// These values are not internal lifecycle statuses.
/// </summary>
public enum InvoiceSubject
{
    Original = 1,
    Correction = 2,
    Cancellation = 3,
    SalesReturn = 4
}

/// <summary>
/// Local identity and tax-authority identifier are distinct. A reference is
/// asserted, never verified, here: Step14-17 must resolve the predecessor
/// and validate ownership, fiscal state, chain, and eligible amounts.
/// </summary>
public sealed record InvoiceReference
{
    public Guid? LocalInvoiceId { get; }
    public string ReferenceTaxId { get; }

    public InvoiceReference(string referenceTaxId, Guid? localInvoiceId = null)
    {
        if (localInvoiceId == Guid.Empty || referenceTaxId is null ||
            referenceTaxId.Length != 22 ||
            referenceTaxId.Any(c => !IsAsciiAlphaNumeric(c)))
            throw new ArgumentException("Invalid referenced fiscal invoice identifier.");
        ReferenceTaxId = referenceTaxId;
        LocalInvoiceId = localInvoiceId;
    }

    private static bool IsAsciiAlphaNumeric(char c) =>
        c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}

/// <summary>
/// All non-original subjects require a reference; original cannot reference
/// anything. A valid shape alone does NOT permit fiscal issuance.
/// </summary>
public sealed record InvoiceSubjectSelection
{
    public InvoiceSubject Subject { get; }
    public InvoiceReference? Reference { get; }

    private InvoiceSubjectSelection(InvoiceSubject subject, InvoiceReference? reference)
    {
        Subject = subject;
        Reference = reference;
    }

    public static InvoiceSubjectSelection Create(InvoiceSubject subject,
        InvoiceReference? reference = null)
    {
        if (!Enum.IsDefined(subject) ||
            subject == InvoiceSubject.Original && reference is not null ||
            subject != InvoiceSubject.Original && reference is null)
            throw new ArgumentException("Invalid subject and reference combination.");
        return new InvoiceSubjectSelection(subject, reference);
    }

    public static InvoiceSubjectSelection Original() => Create(InvoiceSubject.Original);
}


/// <summary>
/// V7.9 p.32 Table 10: export (7), waybill (8), and commodity-linked
/// securities (11) support only original, correction and cancellation.
/// This checks the type/pattern/subject pairing, not predecessor validity.
/// </summary>
public static class OfficialV79InvoiceSubjectPolicy
{
    public static bool Allows(InvoiceTypePattern selection, InvoiceSubject subject)
    {
        if (!Enum.IsDefined(subject)) return false;
        var matrix = OfficialV79InvoicePatternMatrix.Create();
        if (!matrix.Entries.Contains(selection)) return false;
        return !(selection.Type == ElectronicInvoiceType.First &&
                 selection.Pattern is 7 or 8 or 11 &&
                 subject == InvoiceSubject.SalesReturn);
    }
}
