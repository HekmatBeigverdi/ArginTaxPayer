namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Immutable facts about an already-registered fiscal predecessor.
/// These facts MUST be obtained from a trusted, authorized workspace inquiry,
/// not directly from HTTP user input. RC_IITP.IS V7.9 pp15-16,30,32.
/// </summary>
public enum CorrectionReferenceSubject { Original = 1, Correction = 2, SalesReturn = 4 }
public enum CorrectionReferenceResponse { Approved = 1, SystemApproved = 2, NoResponseRequired = 3, Pending = 4, Rejected = 5 }
public enum CorrectionSiblingStatus { Active = 1, Cancelled = 2 }

public sealed record CorrectionReferenceLine
{
    public string GoodsServiceId { get; }
    public decimal Quantity { get; }
    public decimal VatRate { get; }
    public CorrectionReferenceLine(string goodsServiceId, decimal quantity, decimal vatRate)
    {
        if (goodsServiceId is not { Length: 13 } ||
            !goodsServiceId.All(c => c is >= '0' and <= '9') ||
            quantity <= 0m || vatRate < 0m || vatRate > 999.99m ||
            decimal.Round(vatRate, 2, MidpointRounding.ToZero) != vatRate)
            throw new ArgumentException("Invalid registered predecessor line.");
        GoodsServiceId = goodsServiceId;
        Quantity = quantity;
        VatRate = vatRate;
    }
}

/// <summary>
/// Status and lineage are asserted evidence from an external trusted resolver.
/// The Domain cannot query or certify the fiscal portal on its own.
/// </summary>
public sealed class CorrectionPredecessor
{
    public Guid LocalInvoiceId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public string FiscalTaxId { get; }
    public bool IsRegistered { get; }
    public DateTime IssuedAtUtc { get; }
    public InvoiceTypePattern Classification { get; }
    public CorrectionReferenceSubject Subject { get; }
    public CorrectionReferenceResponse Response { get; }
    public bool IsCancelled { get; }
    public string BuyerIdentityFingerprint { get; }
    public IReadOnlyList<CorrectionReferenceLine> Lines { get; }
    public IReadOnlyList<Guid> AncestorInvoiceIds { get; }
    public bool HasActiveCorrectionOrReturn { get; }
    public bool HasConflictingSalesReturn { get; }
    public bool? CommodityExchangeNoticeApproved { get; }

    public CorrectionPredecessor(Guid localInvoiceId, Guid customerUserId,
        Guid taxpayerId, string fiscalTaxId, bool isRegistered, DateTime issuedAtUtc,
        InvoiceTypePattern classification, CorrectionReferenceSubject subject,
        CorrectionReferenceResponse response, bool isCancelled,
        string buyerIdentityFingerprint, IEnumerable<CorrectionReferenceLine> lines,
        IEnumerable<Guid> ancestorInvoiceIds, bool hasActiveCorrectionOrReturn,
        bool hasConflictingSalesReturn, bool? commodityExchangeNoticeApproved = null)
    {
        if (localInvoiceId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || fiscalTaxId is not { Length: 22 } ||
            !fiscalTaxId.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z') ||
            issuedAtUtc.Kind != DateTimeKind.Utc || classification is null ||
            string.IsNullOrWhiteSpace(buyerIdentityFingerprint) ||
            !Enum.IsDefined(subject) || !Enum.IsDefined(response) ||
            lines is null || ancestorInvoiceIds is null)
            throw new ArgumentException("Invalid correction predecessor evidence.");
        var frozenLines = lines.ToArray();
        var frozenAncestors = ancestorInvoiceIds.ToArray();
        if (frozenLines.Any(x => x is null) ||
            frozenLines.Select(x => x.GoodsServiceId).Distinct(StringComparer.Ordinal).Count() != frozenLines.Length ||
            frozenAncestors.Any(x => x == Guid.Empty || x == localInvoiceId) ||
            frozenAncestors.Distinct().Count() != frozenAncestors.Length)
            throw new ArgumentException("Ambiguous predecessor items or cyclic ancestry.");
        LocalInvoiceId = localInvoiceId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        FiscalTaxId = fiscalTaxId;
        IsRegistered = isRegistered;
        IssuedAtUtc = issuedAtUtc;
        Classification = classification;
        Subject = subject;
        Response = response;
        IsCancelled = isCancelled;
        BuyerIdentityFingerprint = buyerIdentityFingerprint;
        Lines = Array.AsReadOnly(frozenLines);
        AncestorInvoiceIds = Array.AsReadOnly(frozenAncestors);
        HasActiveCorrectionOrReturn = hasActiveCorrectionOrReturn;
        HasConflictingSalesReturn = hasConflictingSalesReturn;
        CommodityExchangeNoticeApproved = commodityExchangeNoticeApproved;
    }
}

public sealed record CorrectionDraftLine(string GoodsServiceId,
    decimal Quantity, decimal VatRate);

public sealed class CorrectionDraftEvidence
{
    public Guid InvoiceId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public DateTime IssuedAtUtc { get; }
    public InvoiceTypePattern Classification { get; }
    public InvoiceSubjectSelection SubjectSelection { get; }
    public string BuyerIdentityFingerprint { get; }
    public IReadOnlyList<CorrectionDraftLine> Lines { get; }
    public bool ReductionIsCorrectionOfOriginalMistake { get; }

    public CorrectionDraftEvidence(Guid invoiceId, Guid customerUserId,
        Guid taxpayerId, DateTime issuedAtUtc, InvoiceTypePattern classification,
        InvoiceSubjectSelection subjectSelection, string buyerIdentityFingerprint,
        IEnumerable<CorrectionDraftLine> lines,
        bool reductionIsCorrectionOfOriginalMistake)
    {
        if (invoiceId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || issuedAtUtc.Kind != DateTimeKind.Utc ||
            classification is null || subjectSelection is null ||
            string.IsNullOrWhiteSpace(buyerIdentityFingerprint) || lines is null)
            throw new ArgumentException("Invalid correction draft evidence.");
        InvoiceId = invoiceId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        IssuedAtUtc = issuedAtUtc;
        Classification = classification;
        SubjectSelection = subjectSelection;
        BuyerIdentityFingerprint = buyerIdentityFingerprint;
        Lines = Array.AsReadOnly(lines.ToArray());
        ReductionIsCorrectionOfOriginalMistake = reductionIsCorrectionOfOriginalMistake;
    }
}
