namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Builds the V7.9 cancellation-only wire projection. This does not authorize
/// issuance, reserve a serial, sign, or submit. The calling application MUST
/// verify the predecessor's owner, authoritative SUCCESS, chain and consent.
/// </summary>
public static class Phase10CancellationWireFactory
{
    public static InvoiceWireProjection Project(
        string newlyReservedTaxId, string predecessorTaxId,
        string sellerEconomicCode, long issueUnixMilliseconds,
        InvoiceTypePattern originalClassification)
    {
        if (newlyReservedTaxId == predecessorTaxId)
            throw new ArgumentException("Cancellation must use a new fiscal tax ID.");
        if (!OfficialV79InvoiceSubjectPolicy.Allows(
                originalClassification,InvoiceSubject.Cancellation))
            throw new ArgumentException("Cancellation is unsupported for this invoice pattern.");
        var input = new InvoiceWireInput(
            originalClassification, InvoiceSubject.Cancellation,
            newlyReservedTaxId,issueUnixMilliseconds,sellerEconomicCode,
            Array.Empty<InvoiceWireLine>(),0,0,0,0,0,
            ReferenceTaxId:predecessorTaxId,
            InvoiceNumber:newlyReservedTaxId.Length==22?newlyReservedTaxId.Substring(11,10):null);
        return Phase10InvoiceWireMapper.Project(input);
    }
}
