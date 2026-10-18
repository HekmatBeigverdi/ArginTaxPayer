using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoiceLinesTests
{
    private static readonly DateTime At = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);

    private static InvoiceAggregate Draft(Guid customerId) =>
        InvoiceAggregate.CreateDraft(Guid.NewGuid(), customerId, Guid.NewGuid(), null,
            At, At, new InvoicePartySnapshot(null, "فروشنده"));

    private static InvoiceCatalogHandoff Handoff(Guid customerId,
        CatalogItemKind kind = CatalogItemKind.StockItem) => new(
        InvoiceCatalogHandoffPolicy.CurrentContractVersion,
        customerId, Guid.NewGuid(), 4, "I-007", "کالای داخلی",
        kind, Guid.NewGuid(), "1627", "عدد",
        Guid.NewGuid(), "RC_UMGS.ST", "V1.18", new string('a', 64),
        Guid.NewGuid(), 3, At.AddDays(-1), null,
        Guid.NewGuid(), "1234567890123", "نام رسمی",
        Guid.NewGuid(), "RC_GS", "2026", new string('b', 64),
        At, At, InvoiceVatResolution.Unresolved);

    [Fact]
    public void Captured_line_retains_exact_immutable_official_provenance()
    {
        var customer = Guid.NewGuid();
        var invoice = Draft(customer);
        var original = Handoff(customer);
        invoice.AddDraftLine(Guid.NewGuid(), 1.25m, original);
        var item = Assert.Single(invoice.Lines);
        Assert.Equal(1, item.Position);
        Assert.Equal(1.25m, item.Quantity);
        Assert.Equal(original, item.Catalog);
        Assert.NotSame(original, item.Catalog);
        Assert.Equal(original.OfficialCode, item.OfficialGoodsServiceId);
        Assert.Equal(original.OfficialTitle, item.OfficialGoodsServiceTitle);
        Assert.Equal(original.OfficialUnitCode, item.OfficialMeasurementUnit);
        Assert.Equal(InvoiceVatResolution.Unresolved, item.Catalog.Vat);
        var revisedExternalCatalogValue = original with
        {
            OfficialCode = "9999999999999",
            ItemTitle = "نام اصلاح شده",
            UnitReleaseEdition = "V2"
        };
        Assert.NotEqual(revisedExternalCatalogValue.OfficialCode, item.OfficialGoodsServiceId);
        Assert.Equal("V1.18", item.Catalog.UnitReleaseEdition);
        Assert.Equal(4, item.Catalog.CatalogItemRevision);
        Assert.Equal(3, item.Catalog.MappingRevision);
    }

    [Fact]
    public void Cross_customer_injection_is_rejected_atomically()
    {
        var owner = Guid.NewGuid();
        var invoice = Draft(owner);
        Assert.Throws<ArgumentException>(() =>
            invoice.AddDraftLine(Guid.NewGuid(), 2m, Handoff(Guid.NewGuid())));
        Assert.Empty(invoice.Lines);
        Assert.Equal(1, invoice.Revision);
    }

    [Fact]
    public void Incomplete_invalid_version_and_expired_mapping_are_rejected()
    {
        var customer = Guid.NewGuid();
        var invoice = Draft(customer);
        var valid = Handoff(customer);
        foreach (var invalid in new[]
        {
            valid with { ContractVersion = 2 },
            valid with { OfficialReleaseSha256 = "" },
            valid with { MappingEffectiveToUtc = At.AddDays(-2) },
            valid with { Vat = (InvoiceVatResolution)1 }
        })
            Assert.Throws<ArgumentException>(() =>
                invoice.AddDraftLine(Guid.NewGuid(), 1m, invalid));
        Assert.Empty(invoice.Lines);
        Assert.Equal(1, invoice.Revision);
    }

    [Fact]
    public void Zero_negative_quantities_and_duplicate_identifiers_are_rejected()
    {
        var owner = Guid.NewGuid();
        var invoice = Draft(owner);
        var id = Guid.NewGuid();
        foreach (var amount in new[] { 0m, -1m })
            Assert.Throws<ArgumentException>(() => invoice.AddDraftLine(id, amount, Handoff(owner)));
        invoice.AddDraftLine(id, 2m, Handoff(owner));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.AddDraftLine(id, 1m, Handoff(owner)));
        Assert.Equal(2, invoice.Revision);
        Assert.Single(invoice.Lines);
    }

    [Fact]
    public void Updating_quantity_does_not_replace_source_and_no_op_does_not_change_revision()
    {
        var owner = Guid.NewGuid();
        var invoice = Draft(owner);
        var id = Guid.NewGuid();
        invoice.AddDraftLine(id, 2m, Handoff(owner));
        var oldSnapshot = invoice.Lines[0].Catalog;
        invoice.UpdateDraftLineQuantity(id, 2m);
        Assert.Equal(2, invoice.Revision);
        invoice.UpdateDraftLineQuantity(id, 3.75m);
        Assert.Equal(3, invoice.Revision);
        Assert.Same(oldSnapshot, invoice.Lines[0].Catalog);
        Assert.Equal(3.75m, invoice.Lines[0].Quantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => invoice.UpdateDraftLineQuantity(id, 0m));
        Assert.Equal(3, invoice.Revision);
    }

    [Fact]
    public void Removing_line_reindexes_remaining_lines_without_altering_catalogs()
    {
        var owner = Guid.NewGuid();
        var invoice = Draft(owner);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        invoice.AddDraftLine(a, 1m, Handoff(owner));
        invoice.AddDraftLine(b, 5m, Handoff(owner, CatalogItemKind.Service));
        var lastCatalog = invoice.Lines[1].Catalog;
        invoice.RemoveDraftLine(a);
        Assert.Equal(b, Assert.Single(invoice.Lines).Id);
        Assert.Equal(1, invoice.Lines[0].Position);
        Assert.Same(lastCatalog, invoice.Lines[0].Catalog);
        Assert.Equal(CatalogItemKind.Service, invoice.Lines[0].ItemKind);
        Assert.Equal(4, invoice.Revision);
        Assert.Throws<KeyNotFoundException>(() => invoice.RemoveDraftLine(a));
        Assert.Equal(4, invoice.Revision);
    }

    [Fact]
    public void Exposed_lines_cannot_be_mutated_through_collection_cast()
    {
        var invoice = Draft(Guid.NewGuid());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<InvoiceLineSnapshot>)invoice.Lines).Add(null!));
    }
}
