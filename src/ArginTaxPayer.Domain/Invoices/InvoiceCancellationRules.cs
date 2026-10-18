namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// V7.9 §5.3 pp16-17, Table8 p30 and Table10 p32.
/// Pure preflight validation. All predecessor facts are external assertions;
/// absence of findings never authorizes fiscal issuance or response mutations.
/// </summary>
public static class OfficialV79CancellationRules
{
    private const string Key = "invoice.cancellation.reference";

    public static IReadOnlyList<InvoiceRuleFinding> Evaluate(
        CancellationDraftEvidence draft, CancellationPredecessor reference)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(reference);
        var findings = new List<InvoiceRuleFinding>();
        void Fail(string suffix, string path) =>
            findings.Add(new InvoiceRuleFinding(new InvoiceRuleKey(Key),
                InvoiceRuleSeverity.Error, "invoice.cancellation." + suffix, path));

        if (draft.SubjectSelection.Subject != InvoiceSubject.Cancellation ||
            draft.SubjectSelection.Reference is null)
            Fail("subject_required", "header.ins");

        var claim = draft.SubjectSelection.Reference;
        if (claim is null || claim.ReferenceTaxId != reference.FiscalTaxId ||
            claim.LocalInvoiceId is { } id && id != reference.LocalInvoiceId)
            Fail("reference_mismatch", "header.irtaxid");

        if (!reference.IsRegistered)
            Fail("reference_unregistered", "header.irtaxid");
        if (reference.Subject == InvoiceSubject.Cancellation || reference.IsCancelled)
            Fail("already_cancelled_or_reference_cancellation", "header.irtaxid");
        if (reference.HasExistingCancellationRequest)
            Fail("duplicate_cancellation_request", "header.irtaxid");
        if (draft.InvoiceId == reference.LocalInvoiceId ||
            reference.AncestorIds.Contains(draft.InvoiceId))
            Fail("reference_cycle", "header.irtaxid");

        if (draft.CustomerUserId != reference.CustomerUserId ||
            draft.TaxpayerId != reference.TaxpayerId)
            Fail("workspace_mismatch", "header.irtaxid");

        if (draft.IssuedAtUtc < reference.IssuedAtUtc)
            Fail("issue_before_reference", "header.indatim");

        if (draft.Classification != reference.Classification)
            Fail("internal_classification_mismatch", "header.ins");

        // An approved correction/return may already have made the original
        // reference effectively cancelled. A pending/rejected related invoice
        // is not itself grounds to block cancellation under §5.3 p17.
        if (reference.RelatedDocuments.Any(x => x.IsApproved))
            Fail("approved_related_document", "header.irtaxid");

        if (draft.Payload.TransmittedType is not null ||
            draft.Payload.TransmittedPattern is not null)
            Fail("type_pattern_outside_payload", "header");
        if (draft.Payload.HasBuyerFields)
            Fail("buyer_outside_payload", "header");
        if (draft.Payload.HasBodyLines)
            Fail("body_outside_payload", "body");
        if (draft.Payload.ServiceBillId is not null &&
            reference.Classification.Type == ElectronicInvoiceType.First &&
            reference.Classification.Pattern == 5)
            Fail("bill_id_outside_payload", "header.billid");

        // Cancellation due to nonoccurrence requires documentary evidence;
        // references for other reasons are also required as workflow proof
        // (an internal evidence ID, never raw personal data).
        if (string.IsNullOrWhiteSpace(draft.ReasonEvidenceReference))
            Fail("reason_evidence_required", "internal.cancellationReason");

        // Table10 p32: export correction/cancellation issue time must be
        // not earlier than customs declaration registration time.
        if (reference.Classification.Type == ElectronicInvoiceType.First &&
            reference.Classification.Pattern == 7)
        {
            if (reference.ExportDeclarationAtUtc is null)
                Fail("export_customs_date_unverified", "header.indatim");
            else if (draft.IssuedAtUtc < reference.ExportDeclarationAtUtc.Value)
                Fail("export_before_customs_declaration", "header.indatim");
        }

        return Array.AsReadOnly(findings
            .OrderBy(x => x.FieldPath, StringComparer.Ordinal)
            .ThenBy(x => x.MessageKey, StringComparer.Ordinal).ToArray());
    }
}
