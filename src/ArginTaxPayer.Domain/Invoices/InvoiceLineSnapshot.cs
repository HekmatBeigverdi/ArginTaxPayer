using ArginTaxPayer.Domain.Catalog;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Invoice-owned immutable version-one snapshot of Phase06 catalog handoff.
/// It retains the exact source/release provenance instead of resolving mutable catalog rows.
/// Monetary terms, VAT decisions and invoice payload serialization belong to later steps.
/// </summary>
public sealed record InvoiceLineSnapshot
{
    public Guid Id { get; }
    public int Position { get; }
    public decimal Quantity { get; }
    public InvoiceCatalogHandoff Catalog { get; }

    public string OfficialGoodsServiceId => Catalog.OfficialCode; // V7.9: sstid
    public string OfficialGoodsServiceTitle => Catalog.OfficialTitle; // V7.9: sstt
    public string OfficialMeasurementUnit => Catalog.OfficialUnitCode; // V7.9: mu
    public CatalogItemKind ItemKind => Catalog.ItemKind;

    private InvoiceLineSnapshot(Guid id, int position, decimal quantity, InvoiceCatalogHandoff catalog)
    {
        Id = id;
        Position = position;
        Quantity = quantity;
        Catalog = catalog;
    }

    public static InvoiceLineSnapshot Create(Guid id, int position, decimal quantity,
        InvoiceCatalogHandoff catalog, Guid invoiceCustomerUserId)
    {
        if (id == Guid.Empty || position < 1 || quantity <= 0m ||
            invoiceCustomerUserId == Guid.Empty ||
            !InvoiceCatalogHandoffPolicy.IsComplete(catalog) ||
            catalog.CustomerUserId != invoiceCustomerUserId ||
            !InvoiceCatalogHandoffPolicy.IsEffective(
                catalog.MappingEffectiveFromUtc, catalog.MappingEffectiveToUtc,
                catalog.EffectiveAtUtc))
            throw new ArgumentException("Invalid invoice line or scoped catalog snapshot.");

        // Phase06 immutable positional record consists solely of scalar primitives,
        // enums, strings, Guid and DateTime; no nested mutable collections.
        // Copy the value so the Invoice owns its provenance-bearing reference.
        return new InvoiceLineSnapshot(id, position, quantity, catalog with { });
    }

    internal InvoiceLineSnapshot WithPosition(int position) =>
        new(Id, position, Quantity, Catalog);

    internal InvoiceLineSnapshot WithQuantity(decimal quantity)
    {
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        return new InvoiceLineSnapshot(Id, Position, quantity, Catalog);
    }
}
