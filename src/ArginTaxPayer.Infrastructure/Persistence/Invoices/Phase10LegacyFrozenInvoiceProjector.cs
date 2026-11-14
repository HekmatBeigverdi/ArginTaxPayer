using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Maps a trusted finalized internal invoice revision to the supported
/// Legacy Type 1 or original Type 2 / Pattern 1 wire shape. Fiscal identities and official
/// unit mappings MUST come from authorized current server-side records.
/// No HTTP, signing or dispatch takes place here.
/// </summary>
public static class Phase10LegacyFrozenInvoiceProjector
{
    public static InvoiceWireProjection Project(
        StoredInvoice invoice, string fiscalTaxId,
        string sellerTaxIdentifier, string? buyerTaxIdentifier,
        IReadOnlyDictionary<Guid,string> verifiedUnitCodes, string? invoiceNumber = null,
        int? buyerPersonTypeOverride = null, long? totalOtherTaxAndLegalFeesOverride = null,
        string? buyerNationalOrForeignCode = null, string? buyerBranchCode = null,
        string? buyerPostalCode = null, string? sellerBranchCode = null,
        AuthorityOperationBinding? validatedAuthorityBinding = null)
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
            invoice.Subject is not (InvoiceSubject.Original or InvoiceSubject.Correction or InvoiceSubject.Cancellation or InvoiceSubject.SalesReturn) ||
            invoice.Revision < 1 || invoice.IssueDateUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invoice is not a finalized UTC revision.");
        using var source = JsonDocument.Parse(invoice.SnapshotJson);
        var root = source.RootElement;
        var type2 = InvoiceSnapshotClassification.IsType2GeneralSale(root);
        var contracting = root.TryGetProperty("invoiceType", out var invoiceType) && invoiceType.GetInt32() == 1 &&
            root.TryGetProperty("pattern", out var patternField) && patternField.GetInt32() == 4;
        if (root.ValueKind != JsonValueKind.Object ||
            !HasSupportedEnvironment(invoice, root, validatedAuthorityBinding) ||
            root.GetProperty("sourceWorkspaceId").GetGuid() != invoice.Id ||
            (!type2 && root.GetProperty("invoiceType").GetInt32() != 1) ||
            (!contracting && root.GetProperty("pattern").GetInt32() != 1))
            throw new NotSupportedException("Only an explicitly scoped finalized Type1 or original Type2/Pattern1 snapshot is supported.");

        if ((type2 || contracting) && invoice.Subject != InvoiceSubject.Original)
            throw new NotSupportedException("Only original Type2/Pattern1 invoices are supported.");
        var entries = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            root.GetProperty("lines").GetRawText()) ?? [];
        if (invoice.Subject == InvoiceSubject.SalesReturn)
            entries = entries.Where(item => item.Quantity > 0).ToArray();
        if (entries.Length == 0 || entries.Length > 150)
            throw new ArgumentException("Missing official invoice items.");
        var buyerPersonType = buyerPersonTypeOverride;
        if (!type2 && buyerPersonType is null && root.TryGetProperty("buyerPersonType", out var buyerTypeField) &&
            buyerTypeField.TryGetInt32(out var storedBuyerPersonType))
            buyerPersonType = storedBuyerPersonType;
        if (!type2 && (buyerPersonType is < 1 or > 4 or null))
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

        int? settlement = type2 ? null : root.GetProperty("settlementMethod").GetInt32();
        long? cash = type2 ? null : CheckedRial(root.GetProperty("cashAmountRial").GetDecimal());
        long? credit = type2 ? null : CheckedRial(root.GetProperty("creditAmountRial").GetDecimal());
        var wire = new InvoiceWireInput(
            new InvoiceTypePattern(type2 ? ElectronicInvoiceType.Second : ElectronicInvoiceType.First,contracting ? 4 : 1),
            invoice.Subject,fiscalTaxId,
            new DateTimeOffset(invoice.IssueDateUtc).ToUnixTimeMilliseconds(),
            sellerTaxIdentifier,lines,
            CheckedRial(financial.BeforeDiscountRial),CheckedRial(financial.DiscountRial),
            CheckedRial(financial.AfterDiscountRial),
            CheckedRial(financial.VatRial ?? 0),
            CheckedRial((financial.GrandTotalRial ?? 0) + otherTaxAndLegalFees.Value),
            BuyerTaxId:type2 ? null : buyerTaxIdentifier,
            ContractRegistrationNumber:contracting && root.TryGetProperty("contractNumber", out var contractField)
                && contractField.ValueKind == JsonValueKind.String ? contractField.GetString() : null,
            ReferenceTaxId:invoice.Subject is InvoiceSubject.Correction or InvoiceSubject.SalesReturn
                ?root.GetProperty("referenceTaxId").GetString():null,
            SettlementMethod:settlement,
            // The internal display number is not the authority's 10-hex-digit serial.
            CashAmountRial:cash,CreditAmountRial:credit,InvoiceNumber:fiscalTaxId.Substring(11,10),
            BuyerPersonType:type2 ? null : buyerPersonType,
            TotalOtherTaxAndLegalFeesRial:otherTaxAndLegalFees,
            BuyerNationalOrForeignCode:type2 ? null : buyerNationalOrForeignCode,
            BuyerBranchCode:type2 ? null : buyerBranchCode,BuyerPostalCode:type2 ? null : buyerPostalCode,
            SellerBranchCode:sellerBranchCode);
        return Phase10InvoiceWireMapper.Project(wire);
    }

    private static bool HasSupportedEnvironment(StoredInvoice invoice, JsonElement root,
        AuthorityOperationBinding? production)
    {
        if (!root.TryGetProperty("environment", out var field) || field.ValueKind != JsonValueKind.String)
            return false;
        if (production is null) return field.GetString() == "Sandbox";
        var binding = production.Value;
        return field.GetString() == (binding.Environment == TaxAuthorityEnvironment.Production ? "Production" : "Sandbox") &&
            (invoice.Subject is InvoiceSubject.Original or InvoiceSubject.Correction or
                InvoiceSubject.Cancellation or InvoiceSubject.SalesReturn) &&
            (binding.Environment is TaxAuthorityEnvironment.Production or TaxAuthorityEnvironment.Sandbox) &&
            binding.CustomerUserId != Guid.Empty && binding.CustomerUserId == invoice.CustomerUserId &&
            binding.TaxpayerId != Guid.Empty && binding.TaxpayerId == invoice.TaxpayerId &&
            binding.InvoiceId == invoice.Id && binding.FiscalMemoryId != Guid.Empty &&
            root.TryGetProperty("fiscalMemoryId", out var memory) &&
            memory.TryGetGuid(out var memoryId) && memoryId == binding.FiscalMemoryId;
    }

    private static long CheckedRial(decimal value)
    {
        if (value < 0 || decimal.Truncate(value) != value || value > long.MaxValue)
            throw new ArgumentException("Non-integral or out-of-range rial amount.");
        return checked((long)value);
    }
}
