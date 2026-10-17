namespace ArginTaxPayer.Domain.Invoices;

/// <summary>Cancellation reasons described in RC_IITP.IS V7.9 p16 §5.3.</summary>
public enum CancellationReason
{
    BuyerIdentityChange = 1,
    EntireSaleReturned = 2,
    TransactionDidNotOccur = 3,
    TransactionDateRecordedIncorrectly = 4
}

public enum CancellationRelatedSubject { Correction = 2, SalesReturn = 4 }
public enum CancellationRelatedResponse
{
    AwaitingReaction = 1, Rejected = 2, Approved = 3,
    SystemApproved = 4, NoReactionRequired = 5, Cancelled = 6
}

/// <summary>
/// Trusted immutable facts, not a query to the Tax Authority. Fetch via a
/// tenant-scoped, authenticated resolver and verify freshness in application.
/// </summary>
public sealed record CancellationRelatedDocument(
    Guid LocalInvoiceId, CancellationRelatedSubject Subject,
    CancellationRelatedResponse Response)
{
    public bool IsApproved => Response is CancellationRelatedResponse.Approved or
        CancellationRelatedResponse.SystemApproved or CancellationRelatedResponse.NoReactionRequired;
}

public sealed class CancellationPredecessor
{
    public Guid LocalInvoiceId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public string FiscalTaxId { get; }
    public DateTime IssuedAtUtc { get; }
    public InvoiceTypePattern Classification { get; }
    public InvoiceSubject Subject { get; }
    public bool IsRegistered { get; }
    public bool IsCancelled { get; }
    public bool HasExistingCancellationRequest { get; }
    public IReadOnlyList<Guid> AncestorIds { get; }
    public IReadOnlyList<CancellationRelatedDocument> RelatedDocuments { get; }
    public DateTime? ExportDeclarationAtUtc { get; }

    public CancellationPredecessor(Guid localInvoiceId, Guid customerUserId,
        Guid taxpayerId, string fiscalTaxId, DateTime issuedAtUtc,
        InvoiceTypePattern classification, InvoiceSubject subject,
        bool isRegistered, bool isCancelled, bool hasExistingCancellationRequest,
        IEnumerable<CancellationRelatedDocument> relatedDocuments,
        IEnumerable<Guid> ancestorIds, DateTime? exportDeclarationAtUtc = null)
    {
        if (localInvoiceId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || fiscalTaxId is not { Length: 22 } ||
            !fiscalTaxId.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z') ||
            issuedAtUtc.Kind != DateTimeKind.Utc || classification is null ||
            !Enum.IsDefined(subject) || relatedDocuments is null || ancestorIds is null ||
            exportDeclarationAtUtc is not null && exportDeclarationAtUtc.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid cancellation predecessor evidence.");

        var children = relatedDocuments.ToArray();
        var ancestors = ancestorIds.ToArray();
        if (children.Any(x => x is null || x.LocalInvoiceId == Guid.Empty ||
                !Enum.IsDefined(x.Subject) || !Enum.IsDefined(x.Response)) ||
            children.Select(x => x.LocalInvoiceId).Distinct().Count() != children.Length ||
            ancestors.Any(x => x == Guid.Empty || x == localInvoiceId) ||
            ancestors.Distinct().Count() != ancestors.Length)
            throw new ArgumentException("Duplicate or invalid related records.");

        LocalInvoiceId = localInvoiceId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        FiscalTaxId = fiscalTaxId;
        IssuedAtUtc = issuedAtUtc;
        Classification = classification;
        Subject = subject;
        IsRegistered = isRegistered;
        IsCancelled = isCancelled;
        HasExistingCancellationRequest = hasExistingCancellationRequest;
        RelatedDocuments = Array.AsReadOnly(children);
        AncestorIds = Array.AsReadOnly(ancestors);
        ExportDeclarationAtUtc = exportDeclarationAtUtc;
    }
}

/// <summary>
/// Outbound presence: cancellation's type/pattern, buyer, body and bill
/// identifier are not serialized per V7.9 pp17,32.
/// </summary>
public sealed record CancellationPayloadProjection(
    int? TransmittedType, int? TransmittedPattern,
    bool HasBuyerFields, bool HasBodyLines, string? ServiceBillId);

public sealed class CancellationDraftEvidence
{
    public Guid InvoiceId { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public DateTime IssuedAtUtc { get; }
    public InvoiceTypePattern Classification { get; }
    public InvoiceSubjectSelection SubjectSelection { get; }
    public CancellationReason Reason { get; }
    public string? ReasonEvidenceReference { get; }
    public CancellationPayloadProjection Payload { get; }

    public CancellationDraftEvidence(Guid invoiceId, Guid customerUserId,
        Guid taxpayerId, DateTime issuedAtUtc, InvoiceTypePattern classification,
        InvoiceSubjectSelection subjectSelection, CancellationReason reason,
        string? reasonEvidenceReference, CancellationPayloadProjection payload)
    {
        if (invoiceId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || issuedAtUtc.Kind != DateTimeKind.Utc ||
            classification is null || subjectSelection is null || payload is null ||
            !Enum.IsDefined(reason) ||
            reasonEvidenceReference is { Length: > 160 } ||
            reasonEvidenceReference?.Any(char.IsControl) == true)
            throw new ArgumentException("Invalid cancellation draft.");

        InvoiceId = invoiceId;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        IssuedAtUtc = issuedAtUtc;
        Classification = classification;
        SubjectSelection = subjectSelection;
        Reason = reason;
        ReasonEvidenceReference = reasonEvidenceReference;
        Payload = payload;
    }
}
