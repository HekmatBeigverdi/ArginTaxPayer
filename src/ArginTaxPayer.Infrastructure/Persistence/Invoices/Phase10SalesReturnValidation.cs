using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Validates the physical return quantities against the immutable referenced invoice.
/// Already committed returns (and unresolved in-flight returns) must be accounted for
/// by the caller under a serializable database transaction before issuing a successor.
/// </summary>
public static class Phase10SalesReturnValidation
{
    public static bool AllowedLines(
        IReadOnlyList<InvoiceEntryLine> predecessor,
        IReadOnlyList<InvoiceEntryLine> selected,
        IReadOnlyList<decimal> alreadyReturned)
    {
        if (predecessor.Count == 0 ||
            predecessor.Count != selected.Count ||
            predecessor.Count != alreadyReturned.Count)
            return false;

        var any = false;
        for (var i = 0; i < predecessor.Count; i++)
        {
            var old = predecessor[i];
            var next = selected[i];
            var used = alreadyReturned[i];
            if (old.Quantity <= 0 || used < 0 || used > old.Quantity ||
                next.Quantity < 0 || next.Quantity > old.Quantity - used ||
                next.CatalogItemId != old.CatalogItemId ||
                next.UnitId != old.UnitId ||
                next.OfficialIdentifierSnapshot != old.OfficialIdentifierSnapshot ||
                next.CatalogCodeSnapshot != old.CatalogCodeSnapshot ||
                next.Description != old.Description ||
                next.UnitPriceRial != old.UnitPriceRial ||
                next.DiscountPerUnitRial != old.DiscountPerUnitRial ||
                next.VatRatePercent != old.VatRatePercent)
                return false;
            any |= next.Quantity > 0;
        }
        return any;
    }
}
