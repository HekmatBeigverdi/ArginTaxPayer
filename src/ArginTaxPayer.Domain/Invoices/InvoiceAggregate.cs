using ArginTaxPayer.Domain.Catalog;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Internal document lifecycle; no value signifies tax-authority acceptance.
/// Phase 07 Step 13 will extend transitions after official rules are verified.
/// </summary>
public enum InvoiceLifecycleState { Draft = 1 }

/// <summary>
/// Frozen invoice participant data. The taxpayer/customer ownership relationship
/// must be authorized at the application boundary; user-provided IDs alone do not grant access.
/// </summary>
public sealed record InvoicePartySnapshot
{
    public Guid? PartyId { get; }
    public string DisplayName { get; }
    public string? NationalOrEconomicId { get; }
    public string? PostalCode { get; }

    public InvoicePartySnapshot(Guid? partyId, string displayName,
        string? nationalOrEconomicId = null, string? postalCode = null)
    {
        if (partyId == Guid.Empty || string.IsNullOrWhiteSpace(displayName) ||
            displayName.Length > 256 || displayName.Any(char.IsControl) ||
            !IsOptionalIdentifier(nationalOrEconomicId, 32) ||
            !IsOptionalIdentifier(postalCode, 16))
            throw new ArgumentException("Invalid invoice participant snapshot.");

        PartyId = partyId;
        DisplayName = displayName.Trim();
        NationalOrEconomicId = nationalOrEconomicId?.Trim();
        PostalCode = postalCode?.Trim();
    }

    private static bool IsOptionalIdentifier(string? value, int maxLength) =>
        value is null || (value.Length is > 0 && value.Length <= maxLength &&
                          !value.Any(char.IsControl) && !string.IsNullOrWhiteSpace(value));
}

/// <summary>
/// Domain-only root for a customer-owned taxpayer invoice. No EF configuration,
/// persistence, issuance, fiscal identifier, official type/pattern or VAT inference.
/// All public mutation paths enforce the draft state. Authorization is mandatory
/// in the application layer before constructing or retrieving this aggregate.
/// </summary>
public sealed class InvoiceAggregate
{
    public Guid Id { get; }
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public Guid? FiscalMemoryId { get; }
    public DateTime CreatedAtUtc { get; }
    public DateTime IssueDateUtc { get; private set; }
    public long Revision { get; private set; }
    public InvoiceLifecycleState State { get; private set; }
    public InvoicePartySnapshot Seller { get; private set; }
    public InvoicePartySnapshot? Buyer { get; private set; }
    public InvoiceSubjectSelection SubjectSelection { get; private set; }
    private readonly List<InvoiceLineSnapshot> _lines = new();
    public IReadOnlyList<InvoiceLineSnapshot> Lines => _lines.AsReadOnly();

    private InvoiceAggregate(Guid id, Guid customerUserId, Guid taxpayerId,
        Guid? fiscalMemoryId, DateTime createdAtUtc, DateTime issueDateUtc,
        InvoicePartySnapshot seller, InvoicePartySnapshot? buyer,
        InvoiceSubjectSelection subjectSelection)
    {
        Id = id;
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        FiscalMemoryId = fiscalMemoryId;
        CreatedAtUtc = createdAtUtc;
        IssueDateUtc = issueDateUtc;
        Seller = seller;
        Buyer = buyer;
        SubjectSelection = subjectSelection;
        State = InvoiceLifecycleState.Draft;
        Revision = 1;
    }

    public static InvoiceAggregate CreateDraft(Guid id, Guid customerUserId,
        Guid taxpayerId, Guid? fiscalMemoryId, DateTime createdAtUtc,
        DateTime issueDateUtc, InvoicePartySnapshot seller,
        InvoicePartySnapshot? buyer = null,
        InvoiceSubjectSelection? subjectSelection = null)
    {
        if (id == Guid.Empty || customerUserId == Guid.Empty || taxpayerId == Guid.Empty ||
            fiscalMemoryId == Guid.Empty || seller is null ||
            createdAtUtc.Kind != DateTimeKind.Utc ||
            issueDateUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid invoice identity or UTC dates.");

        var selected = subjectSelection ?? InvoiceSubjectSelection.Original();
        EnsureNotSelfReference(id, selected);
        return new InvoiceAggregate(id, customerUserId, taxpayerId, fiscalMemoryId,
            createdAtUtc, issueDateUtc, seller, buyer, selected);
    }

    public void UpdateDraftDetails(DateTime issueDateUtc, InvoicePartySnapshot seller,
        InvoicePartySnapshot? buyer)
    {
        EnsureDraft();
        if (issueDateUtc.Kind != DateTimeKind.Utc || seller is null)
            throw new ArgumentException("Valid UTC date and seller are required.");

        if (IssueDateUtc == issueDateUtc && Seller == seller && Buyer == buyer)
            return;

        IssueDateUtc = issueDateUtc;
        Seller = seller;
        Buyer = buyer;
        Revision = checked(Revision + 1);
    }

    public void UpdateDraftSubject(InvoiceSubjectSelection subjectSelection)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(subjectSelection);
        EnsureNotSelfReference(Id, subjectSelection);
        if (SubjectSelection == subjectSelection)
            return;
        SubjectSelection = subjectSelection;
        Revision = checked(Revision + 1);
    }

    /// <summary>
    /// Caller must capture the Phase06 handoff using authenticated tenant-scoped
    /// application services. This method validates its customer boundary but
    /// does not replace live authorization checks.
    /// </summary>
    public void AddDraftLine(Guid lineId, decimal quantity, InvoiceCatalogHandoff handoff)
    {
        EnsureDraft();
        if (_lines.Any(x => x.Id == lineId))
            throw new InvalidOperationException("Duplicate invoice line identity.");
        var line = InvoiceLineSnapshot.Create(lineId, _lines.Count + 1,
            quantity, handoff, CustomerUserId);
        _lines.Add(line);
        Revision = checked(Revision + 1);
    }

    public void UpdateDraftLineQuantity(Guid lineId, decimal quantity)
    {
        EnsureDraft();
        var index = _lines.FindIndex(x => x.Id == lineId);
        if (index < 0)
            throw new KeyNotFoundException("Invoice line not found.");
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (_lines[index].Quantity == quantity)
            return;
        _lines[index] = _lines[index].WithQuantity(quantity);
        Revision = checked(Revision + 1);
    }

    public void RemoveDraftLine(Guid lineId)
    {
        EnsureDraft();
        var index = _lines.FindIndex(x => x.Id == lineId);
        if (index < 0)
            throw new KeyNotFoundException("Invoice line not found.");
        _lines.RemoveAt(index);
        for (var i = index; i < _lines.Count; i++)
            _lines[i] = _lines[i].WithPosition(i + 1);
        Revision = checked(Revision + 1);
    }

    private static void EnsureNotSelfReference(Guid id, InvoiceSubjectSelection selection)
    {
        if (selection.Reference?.LocalInvoiceId == id)
            throw new ArgumentException("An invoice cannot refer to itself.");
    }

    private void EnsureDraft()
    {
        if (State != InvoiceLifecycleState.Draft)
            throw new InvalidOperationException("Only a draft invoice may be edited.");
    }
}
