namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// V7.9 pp17-18: a return invoice carries remaining (sold minus returned)
/// lines, not the positive returned quantities. Monetary commercial terms
/// must remain equal to the authoritative predecessor, while totals change
/// only as the remaining quantity changes.
/// </summary>
public sealed record SalesReturnLineTerms
{
    public string GoodsServiceId { get; }
    public decimal RemainingQuantity { get; }
    public decimal VatRatePercent { get; }
    public decimal UnitPriceRial { get; }
    public decimal UnitDiscountRial { get; }

    public SalesReturnLineTerms(string goodsServiceId, decimal remainingQuantity,
        decimal vatRatePercent, decimal unitPriceRial, decimal unitDiscountRial)
    {
        if (goodsServiceId is not { Length: 13 } ||
            !goodsServiceId.All(c => c is >= '0' and <= '9') ||
            remainingQuantity < 0m || remainingQuantity != decimal.Round(remainingQuantity, 8, MidpointRounding.ToZero) ||
            vatRatePercent < 0m || vatRatePercent > 999.99m ||
            vatRatePercent != decimal.Round(vatRatePercent, 2, MidpointRounding.ToZero) ||
            unitPriceRial <= 0m || unitDiscountRial < 0m ||
            unitDiscountRial > unitPriceRial)
            throw new ArgumentException("Invalid return-line evidence.");
        GoodsServiceId = goodsServiceId;
        RemainingQuantity = remainingQuantity;
        VatRatePercent = vatRatePercent;
        UnitPriceRial = unitPriceRial;
        UnitDiscountRial = unitDiscountRial;
    }
}

public enum SalesReturnPredecessorSubject { Original = 1, Correction = 2, SalesReturn = 4 }
public enum SalesReturnPredecessorResponse { Approved = 1, SystemApproved = 2, NoResponseRequired = 3, Pending = 4, Rejected = 5 }

/// <summary>
/// Facts from an authorized, freshness-checked issuer workspace resolver.
/// For chained returns, line quantities MUST represent effective remaining
/// quantities after accepted prior references; never a manually guessed sum.
/// </summary>
public sealed class SalesReturnPredecessor
{
    public Guid LocalInvoiceId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public string FiscalTaxId { get; }
    public bool IsRegistered { get; }
    public bool IsCancelled { get; }
    public DateTime IssuedAtUtc { get; }
    public InvoiceTypePattern Classification { get; }
    public SalesReturnPredecessorSubject Subject { get; }
    public SalesReturnPredecessorResponse Response { get; }
    public string BuyerIdentityFingerprint { get; }
    public bool HasActiveCorrectionOrReturn { get; }
    public IReadOnlyList<Guid> AncestorIds { get; }
    public IReadOnlyList<SalesReturnLineTerms> RemainingLines { get; }

    public SalesReturnPredecessor(Guid localInvoiceId, Guid customerUserId,
        Guid taxpayerId, string fiscalTaxId, bool isRegistered, bool isCancelled,
        DateTime issuedAtUtc, InvoiceTypePattern classification,
        SalesReturnPredecessorSubject subject, SalesReturnPredecessorResponse response,
        string buyerIdentityFingerprint, bool hasActiveCorrectionOrReturn,
        IEnumerable<Guid> ancestorIds, IEnumerable<SalesReturnLineTerms> remainingLines)
    {
        if (localInvoiceId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || fiscalTaxId is not { Length: 22 } ||
            !fiscalTaxId.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z') ||
            issuedAtUtc.Kind != DateTimeKind.Utc || classification is null ||
            !Enum.IsDefined(subject) || !Enum.IsDefined(response) ||
            string.IsNullOrWhiteSpace(buyerIdentityFingerprint) ||
            ancestorIds is null || remainingLines is null)
            throw new ArgumentException("Invalid registered sales-return predecessor.");
        var ancestors = ancestorIds.ToArray();
        var lines = remainingLines.ToArray();
        if (ancestors.Any(x => x == Guid.Empty || x == localInvoiceId) ||
            ancestors.Distinct().Count() != ancestors.Length ||
            lines.Any(x => x is null) ||
            lines.Select(x => x.GoodsServiceId).Distinct(StringComparer.Ordinal).Count() != lines.Length)
            throw new ArgumentException("Ambiguous predecessor chain or lines.");
        LocalInvoiceId = localInvoiceId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        FiscalTaxId = fiscalTaxId;
        IsRegistered = isRegistered;
        IsCancelled = isCancelled;
        IssuedAtUtc = issuedAtUtc;
        Classification = classification;
        Subject = subject;
        Response = response;
        BuyerIdentityFingerprint = buyerIdentityFingerprint;
        HasActiveCorrectionOrReturn = hasActiveCorrectionOrReturn;
        AncestorIds = Array.AsReadOnly(ancestors);
        RemainingLines = Array.AsReadOnly(lines);
    }
}

public sealed class SalesReturnDraftEvidence
{
    public Guid InvoiceId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public DateTime IssuedAtUtc { get; }
    public InvoiceTypePattern Classification { get; }
    public InvoiceSubjectSelection SubjectSelection { get; }
    public string BuyerIdentityFingerprint { get; }
    public IReadOnlyList<SalesReturnLineTerms> RemainingLines { get; }

    public SalesReturnDraftEvidence(Guid invoiceId, Guid customerUserId,
        Guid taxpayerId, DateTime issuedAtUtc, InvoiceTypePattern classification,
        InvoiceSubjectSelection subjectSelection, string buyerIdentityFingerprint,
        IEnumerable<SalesReturnLineTerms> remainingLines)
    {
        if (invoiceId == Guid.Empty || customerUserId == Guid.Empty || taxpayerId == Guid.Empty ||
            issuedAtUtc.Kind != DateTimeKind.Utc || classification is null ||
            subjectSelection is null || string.IsNullOrWhiteSpace(buyerIdentityFingerprint) ||
            remainingLines is null)
            throw new ArgumentException("Invalid sales-return draft.");
        var copy = remainingLines.ToArray();
        if (copy.Any(x => x is null) ||
            copy.Select(x => x.GoodsServiceId).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Ambiguous return line identities.");
        InvoiceId = invoiceId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        IssuedAtUtc = issuedAtUtc;
        Classification = classification;
        SubjectSelection = subjectSelection;
        BuyerIdentityFingerprint = buyerIdentityFingerprint;
        RemainingLines = Array.AsReadOnly(copy);
    }
}
