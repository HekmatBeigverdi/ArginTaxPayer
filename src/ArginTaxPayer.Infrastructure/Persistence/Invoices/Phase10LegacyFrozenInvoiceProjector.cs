using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Maps a trusted finalized internal invoice revision to the supported
/// Legacy Type 1 / Pattern 1 wire shape. Fiscal identities and official
/// unit mappings MUST come from authorized current server-side records.
/// No HTTP, signing or dispatch takes place here.
/// </summary>
public static class Phase10LegacyFrozenInvoiceProjector
{
    public static InvoiceWireProjection Project(
        StoredInvoice invoice, string fiscalTaxId,
        string sellerTaxIdentifier, string buyerTaxIdentifier,
        IReadOnlyDictionary<Guid,string> verifiedUnitCodes, string? invoiceNumber = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(verifiedUnitCodes);
        if (invoice.State != InvoiceLifecycleState.Finalized ||
            invoice.Revision < 1 || invoice.IssueDateUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invoice is not a finalized UTC revision.");
        using var source = JsonDocument.Parse(invoice.SnapshotJson);
        var root = source.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.GetProperty("environment").GetString() != "Sandbox" ||
            root.GetProperty("sourceWorkspaceId").GetGuid() != invoice.Id ||
            root.GetProperty("invoiceType").GetInt32() != 1 ||
            root.GetProperty("pattern").GetInt32() != 1)
            throw new NotSupportedException("Only a finalized Sandbox Type1/Pattern1 snapshot is supported.");

        var entries = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            root.GetProperty("lines").GetRawText()) ?? [];
        if (entries.Length == 0 || entries.Length > 150)
            throw new ArgumentException("Missing official invoice items.");
        var lines = new List<InvoiceWireLine>(entries.Length);
        foreach (var item in entries)
        {
            if (item.CatalogItemId is null || item.UnitId is not { } unitId ||
                !verifiedUnitCodes.TryGetValue(unitId, out var unitCode) ||
                unitCode is not { Length: > 0 } ||
                item.OfficialIdentifierSnapshot is not { Length: 13 } officialId ||
                !officialId.All(char.IsAsciiDigit) ||
                item.Quantity <= 0 || item.UnitPriceRial <= 0 ||
                item.VatRatePercent is null)
                throw new ArgumentException("Official item or unit is incomplete.");

            var gross = CheckedRial(decimal.Round(
                checked(item.Quantity * item.UnitPriceRial), 2, MidpointRounding.AwayFromZero));
            var discount = CheckedRial(decimal.Round(
                checked(item.Quantity * item.DiscountPerUnitRial), 2, MidpointRounding.AwayFromZero));
            var net = checked(gross - discount);
            if (net < 0) throw new ArgumentException("Negative line net amount.");
            var vat = CheckedRial(decimal.Round(
                checked(net * item.VatRatePercent.Value / 100m), 2,
                MidpointRounding.AwayFromZero));
            var total = checked(net + vat);
            lines.Add(new InvoiceWireLine(officialId,item.Quantity,item.UnitPriceRial,
                gross,discount,net,item.VatRatePercent.Value,vat,total,item.Description,unitCode));
        }

        var summary = InvoiceDraftSettlementPolicy.Calculate(entries);
        if (CheckedRial(summary.GrossRial) != lines.Sum(x=>x.BeforeDiscountRial) ||
            CheckedRial(summary.DiscountRial) != lines.Sum(x=>x.DiscountRial) ||
            CheckedRial(summary.PreviewVatRial) != lines.Sum(x=>x.VatRial))
            throw new ArgumentException("Snapshot monetary summary mismatch.");

        var settlement = root.GetProperty("settlementMethod").GetInt32();
        var cash = CheckedRial(root.GetProperty("cashAmountRial").GetDecimal());
        var credit = CheckedRial(root.GetProperty("creditAmountRial").GetDecimal());
        var wire = new InvoiceWireInput(
            new InvoiceTypePattern(ElectronicInvoiceType.First,1),
            InvoiceSubject.Original,fiscalTaxId,
            new DateTimeOffset(invoice.IssueDateUtc).ToUnixTimeMilliseconds(),
            sellerTaxIdentifier,lines,
            CheckedRial(summary.GrossRial),CheckedRial(summary.DiscountRial),
            CheckedRial(summary.AfterDiscountRial),
            CheckedRial(summary.PreviewVatRial),CheckedRial(summary.PreviewGrandTotalRial),
            BuyerTaxId:buyerTaxIdentifier,SettlementMethod:settlement,
            CashAmountRial:cash,CreditAmountRial:credit,InvoiceNumber:invoiceNumber);
        return Phase10InvoiceWireMapper.Project(wire);
    }

    private static long CheckedRial(decimal value)
    {
        if (value < 0 || decimal.Truncate(value) != value || value > long.MaxValue)
            throw new ArgumentException("Non-integral or out-of-range rial amount.");
        return checked((long)value);
    }
}
