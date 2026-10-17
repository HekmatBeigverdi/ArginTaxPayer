using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07CorrectionInvoiceTests
{
    private static readonly DateTime At = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);
    private const string TaxId = "A123456789012345678901";
    private const string Code = "1234567890123";
    private const string OtherCode = "9876543210987";
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Taxpayer = Guid.NewGuid();
    private static readonly Guid PredecessorId = Guid.NewGuid();

    private static CorrectionPredecessor Reference(
        bool registered = true, bool cancelled = false,
        CorrectionReferenceSubject subject = CorrectionReferenceSubject.Original,
        CorrectionReferenceResponse response = CorrectionReferenceResponse.Approved,
        bool conflicting = false, bool returnConflict = false, bool? stockNotice = false,
        Guid? customer = null, Guid? taxpayer = null, IEnumerable<Guid>? ancestors = null) =>
        new(PredecessorId, customer ?? Customer, taxpayer ?? Taxpayer,
            TaxId, registered, At, new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            subject, response, cancelled, "buyer-identity-sha256",
            new[] { new CorrectionReferenceLine(Code, 3m, 10m) },
            ancestors ?? Array.Empty<Guid>(), conflicting, returnConflict, stockNotice);

    private static CorrectionDraftEvidence Draft(
        decimal quantity = 3m, decimal vat = 10m, string code = Code,
        bool reason = false, DateTime? issued = null, Guid? customer = null,
        Guid? taxpayer = null, string buyer = "buyer-identity-sha256",
        InvoiceTypePattern? pattern = null, string referenceId = TaxId,
        Guid? invoiceId = null, IEnumerable<CorrectionDraftLine>? lines = null) =>
        new(invoiceId ?? Guid.NewGuid(), customer ?? Customer, taxpayer ?? Taxpayer,
            issued ?? At.AddMinutes(1),
            pattern ?? new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            InvoiceSubjectSelection.Create(InvoiceSubject.Correction,
                new InvoiceReference(referenceId, PredecessorId)), buyer,
            lines ?? new[] { new CorrectionDraftLine(code, quantity, vat) }, reason);

    private static string[] Errors(CorrectionDraftEvidence draft, CorrectionPredecessor reference) =>
        OfficialV79CorrectionRules.Evaluate(draft, reference)
            .Select(x => x.MessageKey).ToArray();

    [Fact]
    public void Same_fiscal_reference_and_immutable_fields_accept_quantity_increase()
    {
        Assert.Empty(Errors(Draft(quantity: 5m), Reference()));
        Assert.Empty(Errors(Draft(quantity: 2m, reason: true), Reference()));
    }

    [Fact]
    public void Quantity_reduction_requires_claim_of_original_entry_mistake()
    {
        Assert.Contains("invoice.correction.reduction_requires_original_mistake",
            Errors(Draft(quantity: 2m), Reference()));
    }

    [Fact]
    public void Cannot_change_buyer_tax_rate_type_or_pattern()
    {
        Assert.Contains("invoice.correction.buyer_changed",
            Errors(Draft(buyer: "altered-buyer"), Reference()));
        Assert.Contains("invoice.correction.vat_rate_changed",
            Errors(Draft(vat: 9m), Reference()));
        Assert.Contains("invoice.correction.classification_changed",
            Errors(Draft(pattern: new InvoiceTypePattern(ElectronicInvoiceType.First, 3)), Reference()));
    }

    [Fact]
    public void Cannot_add_remove_or_duplicate_goods_service_identifiers()
    {
        Assert.Contains("invoice.correction.new_goods_service_forbidden",
            Errors(Draft(code: OtherCode), Reference()));
        Assert.Contains("invoice.correction.goods_service_removed",
            Errors(Draft(lines: new[] { new CorrectionDraftLine(OtherCode, 2m, 10m) }),
                Reference()));
        Assert.Contains("invoice.correction.duplicate_goods_service",
            Errors(Draft(lines: new[] {
                new CorrectionDraftLine(Code, 3m, 10m),
                new CorrectionDraftLine(Code, 2m, 10m) }), Reference()));
    }

    [Fact]
    public void Unregistered_cancelled_and_other_workspace_references_fail_closed()
    {
        Assert.Contains("invoice.correction.reference_inactive", Errors(Draft(), Reference(registered: false)));
        Assert.Contains("invoice.correction.reference_inactive", Errors(Draft(), Reference(cancelled: true)));
        Assert.Contains("invoice.correction.workspace_mismatch",
            Errors(Draft(), Reference(customer: Guid.NewGuid())));
        Assert.Contains("invoice.correction.workspace_mismatch",
            Errors(Draft(), Reference(taxpayer: Guid.NewGuid())));
    }

    [Fact]
    public void Local_self_reference_and_ancestor_cycles_are_rejected()
    {
        var draft = Draft();
        Assert.Contains("invoice.correction.circular_reference",
            Errors(Draft(invoiceId: PredecessorId), Reference()));
        Assert.Contains("invoice.correction.circular_reference",
            Errors(draft, Reference(ancestors: new[] { draft.InvoiceId })));
    }

    [Fact]
    public void Fiscal_reference_identifier_and_chronology_must_match()
    {
        Assert.Contains("invoice.correction.reference_mismatch",
            Errors(Draft(referenceId: "B123456789012345678901"), Reference()));
        Assert.Contains("invoice.correction.issue_before_reference",
            Errors(Draft(issued: At.AddSeconds(-1)), Reference()));
        Assert.Empty(Errors(Draft(issued: At), Reference()));
    }

    [Fact]
    public void Active_referencing_documents_and_ineligible_responses_are_rejected()
    {
        Assert.Contains("invoice.correction.conflicting_active_reference",
            Errors(Draft(), Reference(conflicting: true)));
        Assert.Contains("invoice.correction.conflicting_active_reference",
            Errors(Draft(), Reference(returnConflict: true)));
        Assert.Contains("invoice.correction.reference_response_ineligible",
            Errors(Draft(), Reference(subject: CorrectionReferenceSubject.Correction,
                response: CorrectionReferenceResponse.Pending)));
        Assert.Empty(Errors(Draft(), Reference(subject: CorrectionReferenceSubject.Correction,
            response: CorrectionReferenceResponse.SystemApproved)));
    }

    [Fact]
    public void Original_and_return_subjects_are_not_eligible_to_be_a_correction_request()
    {
        var valid = Draft();
        var wrong = new CorrectionDraftEvidence(valid.InvoiceId, valid.CustomerUserId,
            valid.TaxpayerId, valid.IssuedAtUtc, valid.Classification,
            InvoiceSubjectSelection.Original(), valid.BuyerIdentityFingerprint,
            valid.Lines, false);
        Assert.Contains("invoice.correction.subject_required", Errors(wrong, Reference()));
    }

    [Fact]
    public void Predecessor_lines_and_ancestry_are_immutable_from_external_lists()
    {
        var list = new List<CorrectionReferenceLine> { new(Code, 3m, 10m) };
        var ancestors = new List<Guid> { Guid.NewGuid() };
        var predecessor = new CorrectionPredecessor(PredecessorId, Customer, Taxpayer,
            TaxId, true, At, new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            CorrectionReferenceSubject.Original, CorrectionReferenceResponse.Approved,
            false, "buyer-identity-sha256", list, ancestors, false, false);
        list.Clear();
        ancestors.Clear();
        Assert.Single(predecessor.Lines);
        Assert.Single(predecessor.AncestorInvoiceIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<CorrectionReferenceLine>)predecessor.Lines).Clear());
    }

    [Fact]
    public void Stock_exchange_notice_requires_explicit_unapproved_evidence()
    {
        var original = Reference(stockNotice: true);
        var exchange = new CorrectionPredecessor(original.LocalInvoiceId,
            original.CustomerUserId, original.TaxpayerId, original.FiscalTaxId,
            original.IsRegistered, original.IssuedAtUtc,
            new InvoiceTypePattern(ElectronicInvoiceType.First, 11),
            original.Subject, original.Response, false,
            original.BuyerIdentityFingerprint, original.Lines,
            Array.Empty<Guid>(), false, false, true);
        var d = Draft(pattern: exchange.Classification);
        Assert.Contains("invoice.correction.stock_exchange_notice_not_unapproved",
            Errors(d, exchange));
    }

    [Fact]
    public void Findings_have_only_stable_keys_not_personal_data()
    {
        var findings = OfficialV79CorrectionRules.Evaluate(
            Draft(buyer: "changed-sensitive-value"), Reference());
        Assert.All(findings, x =>
        {
            Assert.Equal(InvoiceRuleSeverity.Error, x.Severity);
            Assert.Equal("invoice.correction.reference", x.RuleKey.Value);
            Assert.DoesNotContain("changed-sensitive-value", x.MessageKey);
        });
    }
}
