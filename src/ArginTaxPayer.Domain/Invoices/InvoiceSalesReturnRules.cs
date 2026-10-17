namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// RC_IITP.IS V7.9 pp17-18, p16 §5.2, p30 Table8, p32 Table10.
/// A pure preflight check; never a legal authorization to issue or submit.
/// </summary>
public static class OfficialV79SalesReturnRules
{
    private const string Key = "invoice.sales_return.reference";

    public static IReadOnlyList<InvoiceRuleFinding> Evaluate(
        SalesReturnDraftEvidence draft, SalesReturnPredecessor reference)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(reference);
        var findings = new List<InvoiceRuleFinding>();
        void Fail(string suffix, string field) => findings.Add(new InvoiceRuleFinding(
            new InvoiceRuleKey(Key), InvoiceRuleSeverity.Error,
            "invoice.sales_return." + suffix, field));

        if (draft.SubjectSelection.Subject != InvoiceSubject.SalesReturn ||
            draft.SubjectSelection.Reference is null)
            Fail("subject_required", "header.ins");

        var declared = draft.SubjectSelection.Reference;
        if (declared is null || declared.ReferenceTaxId != reference.FiscalTaxId ||
            declared.LocalInvoiceId is { } local && local != reference.LocalInvoiceId)
            Fail("reference_mismatch", "header.irtaxid");
        if (!reference.IsRegistered || reference.IsCancelled)
            Fail("reference_inactive", "header.irtaxid");
        if (draft.CustomerUserId != reference.CustomerUserId ||
            draft.TaxpayerId != reference.TaxpayerId)
            Fail("workspace_mismatch", "header.irtaxid");
        if (draft.InvoiceId == reference.LocalInvoiceId ||
            reference.AncestorIds.Contains(draft.InvoiceId))
            Fail("reference_cycle", "header.irtaxid");
        if (draft.IssuedAtUtc < reference.IssuedAtUtc)
            Fail("issue_before_reference", "header.indatim");
        if (draft.Classification != reference.Classification)
            Fail("classification_changed", "header.inp");
        if (!OfficialV79InvoiceSubjectPolicy.Allows(
                draft.Classification, InvoiceSubject.SalesReturn))
            Fail("pattern_not_eligible", "header.inp");
        if (draft.BuyerIdentityFingerprint != reference.BuyerIdentityFingerprint)
            Fail("buyer_changed", "header");
        if (reference.HasActiveCorrectionOrReturn)
            Fail("active_related_conflict", "header.irtaxid");
        if (reference.Subject is SalesReturnPredecessorSubject.Correction or
                SalesReturnPredecessorSubject.SalesReturn &&
            reference.Response is not (SalesReturnPredecessorResponse.Approved or
                SalesReturnPredecessorResponse.SystemApproved or
                SalesReturnPredecessorResponse.NoResponseRequired))
            Fail("reference_response_ineligible", "header.irtaxid");

        if (reference.RemainingLines.Count == 0 || draft.RemainingLines.Count == 0)
            Fail("lines_missing", "body");
        else
        {
            var previous = reference.RemainingLines.ToDictionary(
                line => line.GoodsServiceId, StringComparer.Ordinal);
            var returned = 0m;
            var positiveRemaining = 0m;
            foreach (var current in draft.RemainingLines)
            {
                if (!previous.TryGetValue(current.GoodsServiceId, out var original))
                {
                    Fail("new_goods_service", "body.sstid");
                    continue;
                }
                if (current.VatRatePercent != original.VatRatePercent)
                    Fail("vat_rate_changed", "body.vra");
                if (current.UnitPriceRial != original.UnitPriceRial ||
                    current.UnitDiscountRial != original.UnitDiscountRial)
                    Fail("commercial_terms_changed", "body.fee");
                if (current.RemainingQuantity > original.RemainingQuantity)
                    Fail("quantity_increased", "body.am");
                else
                    returned = checked(returned +
                        (original.RemainingQuantity - current.RemainingQuantity));
                positiveRemaining = checked(positiveRemaining + current.RemainingQuantity);
            }
            // An omitted predecessor row denotes fully returned quantity; in
            // this draft model the omission is explicit and counts toward
            // the return only when other rows remain positive.
            foreach (var prior in reference.RemainingLines)
                if (!draft.RemainingLines.Any(x => x.GoodsServiceId == prior.GoodsServiceId))
                    returned = checked(returned + prior.RemainingQuantity);
            if (returned <= 0m)
                Fail("quantity_not_reduced", "body.am");
            if (positiveRemaining <= 0m)
                Fail("entire_sale_requires_cancellation", "body");
        }

        return Array.AsReadOnly(findings.OrderBy(x => x.FieldPath, StringComparer.Ordinal)
            .ThenBy(x => x.MessageKey, StringComparer.Ordinal).ToArray());
    }
}
