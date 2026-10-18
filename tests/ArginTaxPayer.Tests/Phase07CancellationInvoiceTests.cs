using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07CancellationInvoiceTests
{
    private static readonly DateTime At = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Taxpayer = Guid.NewGuid();
    private static readonly Guid ReferenceId = Guid.NewGuid();
    private const string TaxId = "A123456789012345678901";

    private static CancellationPredecessor Source(
        InvoiceSubject subject = InvoiceSubject.Original, bool registered = true,
        bool cancelled = false, bool existingCancellation = false,
        IEnumerable<CancellationRelatedDocument>? related = null,
        InvoiceTypePattern? pattern = null, Guid? customer = null,
        Guid? taxpayer = null, IEnumerable<Guid>? ancestors = null,
        DateTime? customs = null) => new(
        ReferenceId, customer ?? Customer, taxpayer ?? Taxpayer, TaxId,
        At, pattern ?? new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
        subject, registered, cancelled, existingCancellation,
        related ?? Array.Empty<CancellationRelatedDocument>(),
        ancestors ?? Array.Empty<Guid>(), customs);

    private static CancellationDraftEvidence Draft(
        InvoiceSubjectSelection? subject = null, Guid? invoiceId = null,
        DateTime? issued = null, Guid? customer = null, Guid? taxpayer = null,
        InvoiceTypePattern? pattern = null, CancellationPayloadProjection? payload = null,
        string? evidence = "case-123") => new(
        invoiceId ?? Guid.NewGuid(), customer ?? Customer, taxpayer ?? Taxpayer,
        issued ?? At.AddMinutes(1),
        pattern ?? new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
        subject ?? InvoiceSubjectSelection.Create(InvoiceSubject.Cancellation,
            new InvoiceReference(TaxId, ReferenceId)),
        CancellationReason.TransactionDidNotOccur, evidence,
        payload ?? new CancellationPayloadProjection(null, null, false, false, null));

    private static string[] Errors(CancellationDraftEvidence draft,
        CancellationPredecessor source) =>
        OfficialV79CancellationRules.Evaluate(draft, source)
            .Select(x => x.MessageKey).ToArray();

    [Fact]
    public void Registered_original_can_be_cancelled_with_frozen_minimal_payload()
    {
        Assert.Empty(Errors(Draft(), Source()));
    }

    [Fact]
    public void Cancellation_requires_subject_and_matching_reference_tax_id()
    {
        Assert.Contains("invoice.cancellation.subject_required",
            Errors(Draft(subject: InvoiceSubjectSelection.Original()), Source()));
        Assert.Contains("invoice.cancellation.reference_mismatch",
            Errors(Draft(subject: InvoiceSubjectSelection.Create(InvoiceSubject.Cancellation,
                new InvoiceReference("B123456789012345678901", ReferenceId))), Source()));
    }

    [Fact]
    public void Unregistered_cancelled_or_previous_cancellation_is_blocked()
    {
        Assert.Contains("invoice.cancellation.reference_unregistered",
            Errors(Draft(), Source(registered: false)));
        Assert.Contains("invoice.cancellation.already_cancelled_or_reference_cancellation",
            Errors(Draft(), Source(cancelled: true)));
        Assert.Contains("invoice.cancellation.already_cancelled_or_reference_cancellation",
            Errors(Draft(), Source(subject: InvoiceSubject.Cancellation)));
        Assert.Contains("invoice.cancellation.duplicate_cancellation_request",
            Errors(Draft(), Source(existingCancellation: true)));
    }

    [Fact]
    public void Different_workspaces_and_cyclic_links_are_rejected()
    {
        Assert.Contains("invoice.cancellation.workspace_mismatch",
            Errors(Draft(), Source(customer: Guid.NewGuid())));
        Assert.Contains("invoice.cancellation.workspace_mismatch",
            Errors(Draft(), Source(taxpayer: Guid.NewGuid())));
        Assert.Contains("invoice.cancellation.reference_cycle",
            Errors(Draft(invoiceId: ReferenceId), Source()));
        var draft = Draft();
        Assert.Contains("invoice.cancellation.reference_cycle",
            Errors(draft, Source(ancestors: new[] { draft.InvoiceId })));
    }

    [Fact]
    public void Inconsistent_issue_date_and_classification_are_rejected()
    {
        Assert.Contains("invoice.cancellation.issue_before_reference",
            Errors(Draft(issued: At.AddSeconds(-1)), Source()));
        Assert.Contains("invoice.cancellation.internal_classification_mismatch",
            Errors(Draft(pattern: new InvoiceTypePattern(ElectronicInvoiceType.First, 3)), Source()));
        Assert.Empty(Errors(Draft(issued: At), Source()));
    }

    [Fact]
    public void Pending_and_rejected_related_documents_are_not_arbitrarily_blocked()
    {
        var pending = new CancellationRelatedDocument(Guid.NewGuid(),
            CancellationRelatedSubject.Correction, CancellationRelatedResponse.AwaitingReaction);
        var rejected = new CancellationRelatedDocument(Guid.NewGuid(),
            CancellationRelatedSubject.SalesReturn, CancellationRelatedResponse.Rejected);
        Assert.Empty(Errors(Draft(), Source(related: new[] { pending, rejected })));
    }

    [Theory]
    [InlineData(CancellationRelatedResponse.Approved)]
    [InlineData(CancellationRelatedResponse.SystemApproved)]
    [InlineData(CancellationRelatedResponse.NoReactionRequired)]
    public void Approved_related_document_prohibits_reusing_cancelled_root(
        CancellationRelatedResponse response)
    {
        Assert.Contains("invoice.cancellation.approved_related_document",
            Errors(Draft(), Source(related: new[]
            {
                new CancellationRelatedDocument(Guid.NewGuid(),
                    CancellationRelatedSubject.Correction, response)
            })));
    }

    [Fact]
    public void Transmitted_type_pattern_buyer_and_body_must_be_absent()
    {
        var projection = new CancellationPayloadProjection(1, 1, true, true, null);
        var errors = Errors(Draft(payload: projection), Source());
        Assert.Contains("invoice.cancellation.type_pattern_outside_payload", errors);
        Assert.Contains("invoice.cancellation.buyer_outside_payload", errors);
        Assert.Contains("invoice.cancellation.body_outside_payload", errors);
    }

    [Fact]
    public void Utility_bill_identifier_is_outside_payload_on_cancellation()
    {
        var p = new InvoiceTypePattern(ElectronicInvoiceType.First, 5);
        var body = new CancellationPayloadProjection(null, null, false, false, "12345");
        Assert.Contains("invoice.cancellation.bill_id_outside_payload",
            Errors(Draft(pattern: p, payload: body), Source(pattern: p)));
    }

    [Fact]
    public void Export_cancellation_requires_declared_customs_chronology()
    {
        var p = new InvoiceTypePattern(ElectronicInvoiceType.First, 7);
        var draft = Draft(pattern: p);
        Assert.Contains("invoice.cancellation.export_customs_date_unverified",
            Errors(draft, Source(pattern: p)));
        Assert.Contains("invoice.cancellation.export_before_customs_declaration",
            Errors(draft, Source(pattern: p, customs: At.AddMinutes(2))));
        Assert.Empty(Errors(draft, Source(pattern: p, customs: At.AddMinutes(-1))));
    }

    [Fact]
    public void Cancellation_reason_must_have_documentary_evidence_reference()
    {
        Assert.Contains("invoice.cancellation.reason_evidence_required",
            Errors(Draft(evidence: null), Source()));
    }

    [Fact]
    public void Related_document_snapshot_is_immutable_and_replay_is_deterministic()
    {
        var children = new List<CancellationRelatedDocument>
        {
            new(Guid.NewGuid(), CancellationRelatedSubject.Correction,
                CancellationRelatedResponse.AwaitingReaction)
        };
        var src = Source(related: children);
        children.Clear();
        Assert.Single(src.RelatedDocuments);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<CancellationRelatedDocument>)src.RelatedDocuments).Clear());
        var d = Draft();
        var a = OfficialV79CancellationRules.Evaluate(d, src);
        var b = OfficialV79CancellationRules.Evaluate(d, src);
        Assert.Equal(a, b);
    }

    [Fact]
    public void No_check_can_claim_tax_authority_acceptance_or_mutate_original()
    {
        var src = Source();
        var d = Draft();
        Assert.Empty(Errors(d, src));
        Assert.Equal(InvoiceSubject.Original, src.Subject);
        Assert.True(src.IsRegistered);
        Assert.False(src.IsCancelled);
    }
}
