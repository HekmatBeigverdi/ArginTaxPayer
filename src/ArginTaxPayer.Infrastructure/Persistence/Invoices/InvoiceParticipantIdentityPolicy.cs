namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

internal static class InvoiceParticipantIdentityPolicy
{
    public static string? MissingCode(string? sellerIdentifier, string? buyerIdentifier,
        bool buyerRequired)
    {
        if (string.IsNullOrWhiteSpace(sellerIdentifier))
            return "seller_economic_identifier_missing";
        if (buyerRequired && string.IsNullOrWhiteSpace(buyerIdentifier))
            return "buyer_identity_missing";
        return null;
    }
}
