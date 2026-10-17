namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// RC_IITP.IS V7.9 pp15-16: immutable type/pattern, buyer, goods IDs and VAT;
/// quantity may increase; reduction is limited to original quantity mistake.
/// p30 Table8: registered predecessor, workspace/date/chain/conflict rules.
/// p32 Table10: commodity exchange approved notice cannot be corrected.
/// </summary>
public static class OfficialV79CorrectionRules
{
    public static IReadOnlyList<InvoiceRuleFinding> Evaluate(
        CorrectionDraftEvidence draft, CorrectionPredecessor reference)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(reference);
        const string key = "invoice.correction.reference";
        var findings = new List<InvoiceRuleFinding>();
        void Add(string message, string path) =>
            findings.Add(new InvoiceRuleFinding(new InvoiceRuleKey(key),
                InvoiceRuleSeverity.Error, message, path));

        if (draft.SubjectSelection.Subject != InvoiceSubject.Correction ||
            draft.SubjectSelection.Reference is null)
            Add("invoice.correction.subject_required", "header.ins");

        var asserted = draft.SubjectSelection.Reference;
        if (asserted is null || asserted.ReferenceTaxId != reference.FiscalTaxId ||
            asserted.LocalInvoiceId is { } local && local != reference.LocalInvoiceId)
            Add("invoice.correction.reference_mismatch", "header.irtaxid");
        if (!reference.IsRegistered || reference.IsCancelled)
            Add("invoice.correction.reference_inactive", "header.irtaxid");
        if (draft.InvoiceId == reference.LocalInvoiceId ||
            reference.AncestorInvoiceIds.Contains(draft.InvoiceId))
            Add("invoice.correction.circular_reference", "header.irtaxid");
        if (draft.CustomerUserId != reference.CustomerUserId ||
            draft.TaxpayerId != reference.TaxpayerId)
            Add("invoice.correction.workspace_mismatch", "header.irtaxid");
        if (draft.IssuedAtUtc < reference.IssuedAtUtc)
            Add("invoice.correction.issue_before_reference", "header.indatim");
        if (draft.Classification != reference.Classification)
            Add("invoice.correction.classification_changed", "header.inp");
        if (draft.BuyerIdentityFingerprint != reference.BuyerIdentityFingerprint)
            Add("invoice.correction.buyer_changed", "header");
        if (reference.HasConflictingSalesReturn || reference.HasActiveCorrectionOrReturn)
            Add("invoice.correction.conflicting_active_reference", "header.irtaxid");
        if (reference.Subject is CorrectionReferenceSubject.Correction or CorrectionReferenceSubject.SalesReturn &&
            reference.Response is not (CorrectionReferenceResponse.Approved or
                CorrectionReferenceResponse.SystemApproved or CorrectionReferenceResponse.NoResponseRequired))
            Add("invoice.correction.reference_response_ineligible", "header.irtaxid");

        if (reference.Classification.Type == ElectronicInvoiceType.First &&
            reference.Classification.Pattern == 11 &&
            reference.CommodityExchangeNoticeApproved is not false)
            Add("invoice.correction.stock_exchange_notice_not_unapproved", "header.irtaxid");
        if (reference.Classification.Type == ElectronicInvoiceType.First &&
            reference.Classification.Pattern == 7 &&
            draft.IssuedAtUtc < reference.IssuedAtUtc)
            Add("invoice.correction.export_chronology", "header.indatim");

        if (draft.Lines.Count == 0 || reference.Lines.Count == 0)
            Add("invoice.correction.lines_required", "body");
        else
        {
            var actual = new Dictionary<string, CorrectionDraftLine>(StringComparer.Ordinal);
            for (var i = 0; i < draft.Lines.Count; i++)
            {
                var line = draft.Lines[i];
                if (line is null || line.GoodsServiceId is not { Length: 13 } ||
                    !line.GoodsServiceId.All(c => c is >= '0' and <= '9') ||
                    line.Quantity <= 0m || line.VatRate < 0m ||
                    line.VatRate > 999.99m ||
                    decimal.Round(line.VatRate, 2, MidpointRounding.ToZero) != line.VatRate)
                {
                    Add("invoice.correction.invalid_line", $"body[{i}]");
                    continue;
                }
                if (!actual.TryAdd(line.GoodsServiceId, line))
                    Add("invoice.correction.duplicate_goods_service", $"body[{i}].sstid");
            }

            var originals = reference.Lines.ToDictionary(x => x.GoodsServiceId,
                StringComparer.Ordinal);
            foreach (var (code, line) in actual)
            {
                if (!originals.TryGetValue(code, out var baseline))
                {
                    Add("invoice.correction.new_goods_service_forbidden", "body.sstid");
                    continue;
                }
                if (line.VatRate != baseline.VatRate)
                    Add("invoice.correction.vat_rate_changed", "body.vra");
                if (line.Quantity < baseline.Quantity &&
                    !draft.ReductionIsCorrectionOfOriginalMistake)
                    Add("invoice.correction.reduction_requires_original_mistake", "body.am");
            }
            foreach (var original in reference.Lines)
                if (!actual.ContainsKey(original.GoodsServiceId))
                    Add("invoice.correction.goods_service_removed", "body.sstid");
        }

        return Array.AsReadOnly(findings.OrderBy(x => x.FieldPath, StringComparer.Ordinal)
            .ThenBy(x => x.MessageKey, StringComparer.Ordinal).ToArray());
    }
}
