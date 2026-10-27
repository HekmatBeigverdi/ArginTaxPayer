using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// A deterministic V7.9 DTO projection for Phase10's three candidate type/patterns.
/// Caller supplies trusted immutable server-side values; this is NOT submission
/// authorization or a replacement for Step07's full regulatory preflight.
/// </summary>
public sealed record InvoiceWireLine(
    string GoodsServiceId, decimal Quantity, decimal UnitPriceRial,
    long BeforeDiscountRial, long DiscountRial, long AfterDiscountRial,
    decimal VatRatePercent, long VatRial, long TotalRial,
    string? Title = null, string? UnitCode = null);

public sealed record InvoiceWirePayment(string? TerminalId, string? TransactionId,
    long? PaidAmountRial);

public sealed record InvoiceWireInput(
    InvoiceTypePattern Classification, InvoiceSubject Subject,
    string TaxId, long IssueUnixMilliseconds, string SellerTaxId,
    IReadOnlyList<InvoiceWireLine> Lines, long TotalBeforeDiscountRial,
    long TotalDiscountRial, long TotalAfterDiscountRial,
    long TotalVatRial, long TotalBillRial,
    string? BuyerTaxId = null, string? ContractRegistrationNumber = null,
    string? ReferenceTaxId = null, int? SettlementMethod = null,
    IReadOnlyList<InvoiceWirePayment>? Payments = null,
    long? CashAmountRial = null, long? CreditAmountRial = null,
    string? InvoiceNumber = null, int? BuyerPersonType = null,
    long? TotalOtherTaxAndLegalFeesRial = null,
    string? BuyerNationalOrForeignCode = null, string? BuyerBranchCode = null,
    string? BuyerPostalCode = null, string? SellerBranchCode = null);

/// <summary>Separate from official SDK's potentially changing public classes.</summary>
public sealed record InvoiceWireProjection(string Json, InvoiceTypePattern Classification,
    InvoiceSubject Subject, string RuleSetEdition, string MapperVersion)
{
    public bool IsSubmissionAuthorized => false;
}

public static class Phase10InvoiceWireMapper
{
    public const string MapperVersion = "phase10-v79-projection-1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static InvoiceWireProjection Project(InvoiceWireInput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var pair = source.Classification;
        if (pair != new InvoiceTypePattern(ElectronicInvoiceType.First, 1) &&
            pair != new InvoiceTypePattern(ElectronicInvoiceType.Second, 1) &&
            pair != new InvoiceTypePattern(ElectronicInvoiceType.First, 4))
            throw new NotSupportedException("Only the two approved Phase10 pattern families are mapped.");
        if (!OfficialV79InvoiceSubjectPolicy.Allows(pair, source.Subject))
            throw new ArgumentException("Unsupported invoice subject.");
        var otherTaxAndLegalFeesRial = source.TotalOtherTaxAndLegalFeesRial ?? 0L;
        if (!IsTaxId(source.TaxId) || !IsDigits(source.SellerTaxId, 1, 32) ||
            source.IssueUnixMilliseconds <= 0 ||
            source.TotalBeforeDiscountRial < 0 || source.TotalDiscountRial < 0 ||
            source.TotalAfterDiscountRial < 0 || source.TotalVatRial < 0 ||
            source.TotalBillRial < 0 ||
            source.TotalBeforeDiscountRial - source.TotalDiscountRial != source.TotalAfterDiscountRial ||
            (decimal)source.TotalAfterDiscountRial + source.TotalVatRial +
                otherTaxAndLegalFeesRial > long.MaxValue ||
            source.TotalBillRial < source.TotalAfterDiscountRial + source.TotalVatRial +
                otherTaxAndLegalFeesRial)
            throw new ArgumentException("Invalid frozen header or monetary totals.");

        if (source.Subject == InvoiceSubject.Original && source.ReferenceTaxId is not null ||
            source.Subject != InvoiceSubject.Original && !IsTaxId(source.ReferenceTaxId))
            throw new ArgumentException("Invalid original/reference relationship.");
        var isContract = pair == new InvoiceTypePattern(ElectronicInvoiceType.First, 4);
        if (isContract && !IsDigits(source.ContractRegistrationNumber, 1, 12) ||
            !isContract && source.ContractRegistrationNumber is not null)
            throw new ArgumentException("Invalid contracting registration number.");
        if (pair.Type == ElectronicInvoiceType.Second && source.BuyerTaxId is not null)
            throw new ArgumentException("Type2 buyer-specific fields require separate verified evidence.");
        if (source.BuyerTaxId is not null && !IsDigits(source.BuyerTaxId, 1, 32))
            throw new ArgumentException("Invalid buyer identifier.");
        if (source.BuyerNationalOrForeignCode is not null &&
            !IsDigits(source.BuyerNationalOrForeignCode, 1, 32))
            throw new ArgumentException("Invalid buyer national or foreign identifier.");
        if (source.BuyerBranchCode is not null && !IsDigits(source.BuyerBranchCode, 1, 16) ||
            source.SellerBranchCode is not null && !IsDigits(source.SellerBranchCode, 1, 16) ||
            source.BuyerPostalCode is not null && !IsDigits(source.BuyerPostalCode, 10, 10))
            throw new ArgumentException("Invalid participant branch or postal code.");
        if (source.BuyerPersonType is < 1 or > 4)
            throw new ArgumentException("Invalid buyer person type.");
        if (otherTaxAndLegalFeesRial < 0)
            throw new ArgumentException("Invalid other tax and legal fee total.");

        var cancellation = source.Subject == InvoiceSubject.Cancellation;
        if (!cancellation && (source.Lines is null || source.Lines.Count == 0))
            throw new ArgumentException("Invoice items required.");
        if (cancellation && source.Lines is { Count: > 0 })
            throw new ArgumentException("Cancellation line projection is not verified.");
        if (source.Payments is { Count: > 0 })
            throw new NotSupportedException("Payment transaction wire mapping awaits official Step07 rules.");
        if (source.SettlementMethod is { } settlement)
        {
            if (settlement is < 1 or > 3 ||
                source.CashAmountRial is not { } cash ||
                source.CreditAmountRial is not { } credit ||
                cash < 0 || credit < 0 ||
                cash > long.MaxValue - credit ||
                cash + credit != source.TotalBillRial ||
                settlement == 1 && (credit != 0 || cash != source.TotalBillRial) ||
                settlement == 2 && (cash != 0 || credit != source.TotalBillRial) ||
                settlement == 3 && (cash <= 0 || credit <= 0))
                throw new ArgumentException("Settlement amounts do not match invoice totals.");
        }
        else if (source.CashAmountRial is not null || source.CreditAmountRial is not null)
            throw new ArgumentException("Settlement amounts require a settlement method.");
        if (source.InvoiceNumber is not null &&
            (source.InvoiceNumber.Length is < 1 or > 40 ||
             source.InvoiceNumber.Any(char.IsControl)))
            throw new ArgumentException("Invalid seller invoice number.");

        var body = cancellation ? Array.Empty<object>() :
            source.Lines!.Select(x =>
            {
                if (!IsDigits(x.GoodsServiceId, 13, 13) || x.Quantity <= 0 ||
                    x.UnitPriceRial <= 0 || x.BeforeDiscountRial < 0 ||
                    x.DiscountRial < 0 || x.AfterDiscountRial < 0 || x.VatRial < 0 ||
                    x.VatRatePercent < 0 || x.TotalRial < 0 ||
                    x.BeforeDiscountRial - x.DiscountRial != x.AfterDiscountRial ||
                    x.Title is { Length: > 400 } ||
                    x.UnitCode is not null && !IsDigits(x.UnitCode, 1, 8))
                    throw new ArgumentException("Invalid frozen item.");
                return (object)new
                {
                    sstid = x.GoodsServiceId,
                    sstt = x.Title,
                    mu = x.UnitCode,
                    am = x.Quantity,
                    fee = x.UnitPriceRial,
                    prdis = x.BeforeDiscountRial,
                    dis = x.DiscountRial,
                    adis = x.AfterDiscountRial,
                    vra = x.VatRatePercent,
                    vam = x.VatRial,
                    tsstam = x.TotalRial
                };
            }).ToArray();
        if (!cancellation &&
            (body.Length != source.Lines!.Count ||
             source.Lines.Sum(x => (decimal)x.BeforeDiscountRial) != source.TotalBeforeDiscountRial ||
             source.Lines.Sum(x => (decimal)x.DiscountRial) != source.TotalDiscountRial ||
             source.Lines.Sum(x => (decimal)x.AfterDiscountRial) != source.TotalAfterDiscountRial ||
             source.Lines.Sum(x => (decimal)x.VatRial) != source.TotalVatRial ||
            source.Lines.Sum(x => (decimal)x.TotalRial) + otherTaxAndLegalFeesRial != source.TotalBillRial))
            throw new ArgumentException("Header and line totals mismatch.");

        // Anonymous ordered members preserve wire property names without introducing
        // a dependency on the archived SDK. Nulls are omitted (not sent as JSON null).
        var dto = new
        {
            header = new
            {
                taxid = source.TaxId,
                inno = source.InvoiceNumber,
                indatim = source.IssueUnixMilliseconds,
                ins = (int)source.Subject,
                inty = cancellation ? (int?)null : (int)pair.Type,
                inp = cancellation ? (int?)null : pair.Pattern,
                tins = source.SellerTaxId,
                tinb = source.BuyerTaxId,
                bid = source.BuyerNationalOrForeignCode,
                bbc = source.BuyerBranchCode,
                bpc = source.BuyerPostalCode,
                sbc = source.SellerBranchCode,
                crn = source.ContractRegistrationNumber,
                irtaxid = source.ReferenceTaxId,
                tprdis = source.TotalBeforeDiscountRial,
                tdis = source.TotalDiscountRial,
                tadis = source.TotalAfterDiscountRial,
                tvam = source.TotalVatRial,
                todam = otherTaxAndLegalFeesRial,
                tbill = source.TotalBillRial,
                tob = source.BuyerPersonType,
                setm = source.SettlementMethod,
                cap = source.CashAmountRial,
                insp = source.CreditAmountRial
            },
            body,
            payments = Array.Empty<object>(),
            extension = Array.Empty<object>()
        };

        return new(JsonSerializer.Serialize(dto, JsonOptions), pair, source.Subject,
            OfficialV79InvoicePatternMatrix.Edition, MapperVersion);
    }

    private static bool IsDigits(string? s, int min, int max) =>
        s is not null && s.Length >= min && s.Length <= max &&
        s.All(c => c is >= '0' and <= '9');
    private static bool IsTaxId(string? s) =>
        s is { Length: 22 } && s.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z');
}
