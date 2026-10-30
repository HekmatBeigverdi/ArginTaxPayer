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
        IReadOnlyDictionary<Guid,string> verifiedUnitCodes, string? invoiceNumber = null,
        int? buyerPersonTypeOverride = null, long? totalOtherTaxAndLegalFeesOverride = null,
        string? buyerNationalOrForeignCode = null, string? buyerBranchCode = null,
        string? buyerPostalCode = null, string? sellerBranchCode = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(verifiedUnitCodes);
        if (sellerTaxIdentifier is not { Length: 11 or 14 } ||
            !sellerTaxIdentifier.All(char.IsAsciiDigit))
            throw new ArgumentException("Legacy seller economic identifier must contain 11 or 14 digits.");
        if (fiscalTaxId is not { Length: 22 } ||
            !fiscalTaxId.AsSpan(11, 10).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid reserved fiscal invoice serial.");
        if (invoice.State != InvoiceLifecycleState.Finalized ||
            invoice.Subject is not (InvoiceSubject.Original or InvoiceSubject.Cancellation) ||
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
        var buyerPersonType = buyerPersonTypeOverride;
        if (buyerPersonType is null && root.TryGetProperty("buyerPersonType", out var buyerTypeField) &&
            buyerTypeField.TryGetInt32(out var storedBuyerPersonType))
            buyerPersonType = storedBuyerPersonType;
        if (buyerPersonType is < 1 or > 4 or null)
            throw new ArgumentException("Buyer person type is required.");

        var otherTaxAndLegalFees = totalOtherTaxAndLegalFeesOverride;
        if (otherTaxAndLegalFees is null &&
            root.TryGetProperty("otherTaxAndLegalFeesRial", out var otherTaxField))
        {
            if (!otherTaxField.TryGetDecimal(out var storedOtherTaxAndLegalFees) ||
                storedOtherTaxAndLegalFees < 0 ||
                storedOtherTaxAndLegalFees != decimal.Truncate(storedOtherTaxAndLegalFees) ||
                storedOtherTaxAndLegalFees > long.MaxValue)
                throw new ArgumentException("Other tax and legal fee total is invalid.");
            otherTaxAndLegalFees = (long)storedOtherTaxAndLegalFees;
        }
        otherTaxAndLegalFees ??= 0;
        if (otherTaxAndLegalFees < 0)
            throw new ArgumentException("Other tax and legal fee total is invalid.");

        var financialInputs = entries.Select(item => new InvoiceFinancialInput(
            item.CatalogItemId ?? Guid.Empty, item.Quantity, item.UnitPriceRial,
            decimal.Truncate(item.Quantity * item.DiscountPerUnitRial), item.VatRatePercent))
            .ToArray();
        if (entries.Any(item =>
                decimal.Truncate(item.Quantity * item.UnitPriceRial) != item.Quantity * item.UnitPriceRial ||
                decimal.Truncate(item.Quantity * item.DiscountPerUnitRial) !=
                    item.Quantity * item.DiscountPerUnitRial))
            throw new ArgumentException("Snapshot line rial amounts must be whole numbers.");
        InvoiceFinancialPreview financial;
        try
        {
            financial = InvoiceFinancialCalculator.Preview(financialInputs,
                new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
                new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition));
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new ArgumentException("Snapshot monetary calculation is invalid.", exception);
        }

        if (invoice.Subject == InvoiceSubject.Cancellation)
        {
            if (!root.TryGetProperty("referenceTaxId", out var refField) ||
                refField.ValueKind != JsonValueKind.String ||
                refField.GetString() is not { Length: 22 } predecessor ||
                predecessor == fiscalTaxId)
                throw new ArgumentException("Verified cancellation predecessor is required.");
            return Phase10CancellationWireFactory.Project(
                fiscalTaxId, predecessor, sellerTaxIdentifier,
                new DateTimeOffset(invoice.IssueDateUtc).ToUnixTimeMilliseconds(),
                new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
                CheckedRial(financial.BeforeDiscountRial),
                CheckedRial(financial.DiscountRial),
                CheckedRial(financial.AfterDiscountRial),
                CheckedRial(financial.VatRial ?? 0),
                CheckedRial((financial.GrandTotalRial ?? 0) + otherTaxAndLegalFees.Value),
                otherTaxAndLegalFees.Value);
        }

        var lines = new List<InvoiceWireLine>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            var item = entries[index];
            if (item.CatalogItemId is null || item.UnitId is not { } unitId ||
                !verifiedUnitCodes.TryGetValue(unitId, out var unitCode) ||
                unitCode is not { Length: > 0 } ||
                item.OfficialIdentifierSnapshot is not { Length: 13 } officialId ||
                !officialId.All(char.IsAsciiDigit) ||
                item.Quantity <= 0 || item.UnitPriceRial <= 0 ||
                item.VatRatePercent is null)
                throw new ArgumentException("Official item or unit is incomplete.");
            var calculated = financial.Lines[index];
            lines.Add(new InvoiceWireLine(officialId,item.Quantity,item.UnitPriceRial,
                CheckedRial(calculated.BeforeDiscountRial),
                CheckedRial(calculated.DiscountRial),
                CheckedRial(calculated.AfterDiscountRial),item.VatRatePercent.Value,
                CheckedRial(calculated.VatRial ?? 0),CheckedRial(calculated.TotalRial ?? 0),
                item.Description,unitCode));
        }

        var settlement = root.GetProperty("settlementMethod").GetInt32();
        var cash = CheckedRial(root.GetProperty("cashAmountRial").GetDecimal());
        var credit = CheckedRial(root.GetProperty("creditAmountRial").GetDecimal());
        var wire = new InvoiceWireInput(
            new InvoiceTypePattern(ElectronicInvoiceType.First,1),
            InvoiceSubject.Original,fiscalTaxId,
            new DateTimeOffset(invoice.IssueDateUtc).ToUnixTimeMilliseconds(),
            sellerTaxIdentifier,lines,
            CheckedRial(financial.BeforeDiscountRial),CheckedRial(financial.DiscountRial),
            CheckedRial(financial.AfterDiscountRial),
            CheckedRial(financial.VatRial ?? 0),
            CheckedRial((financial.GrandTotalRial ?? 0) + otherTaxAndLegalFees.Value),
            BuyerTaxId:buyerTaxIdentifier,SettlementMethod:settlement,
            // The internal display number is not the authority's 10-hex-digit serial.
            CashAmountRial:cash,CreditAmountRial:credit,InvoiceNumber:fiscalTaxId.Substring(11,10),
            BuyerPersonType:buyerPersonType,
            TotalOtherTaxAndLegalFeesRial:otherTaxAndLegalFees,
            BuyerNationalOrForeignCode:buyerNationalOrForeignCode,
            BuyerBranchCode:buyerBranchCode,BuyerPostalCode:buyerPostalCode,
            SellerBranchCode:sellerBranchCode);
        return Phase10InvoiceWireMapper.Project(wire);
    }

    private static long CheckedRial(decimal value)
    {
        if (value < 0 || decimal.Truncate(value) != value || value > long.MaxValue)
            throw new ArgumentException("Non-integral or out-of-range rial amount.");
        return checked((long)value);
    }
}
