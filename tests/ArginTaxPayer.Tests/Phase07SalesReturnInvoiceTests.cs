using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07SalesReturnInvoiceTests
{
    private static readonly DateTime At = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Taxpayer = Guid.NewGuid();
    private static readonly Guid PredecessorId = Guid.NewGuid();
    private const string TaxId = "A123456789012345678901";
    private const string A = "1234567890123";
    private const string B = "9876543210987";

    private static SalesReturnLineTerms Line(string id, decimal qty, decimal vat = 10m,
        decimal price = 100m, decimal discount = 5m) =>
        new(id, qty, vat, price, discount);

    private static SalesReturnPredecessor Source(
        IEnumerable<SalesReturnLineTerms>? lines = null,
        SalesReturnPredecessorSubject subject = SalesReturnPredecessorSubject.Original,
        SalesReturnPredecessorResponse response = SalesReturnPredecessorResponse.Approved,
        bool registered = true, bool cancelled = false, bool activeSibling = false,
        Guid? owner = null, Guid? taxpayer = null, IEnumerable<Guid>? ancestors = null,
        InvoiceTypePattern? pattern = null) =>
        new(PredecessorId, owner ?? Owner, taxpayer ?? Taxpayer, TaxId,
            registered, cancelled, At, pattern ??
            new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            subject, response, "buyer-fingerprint", activeSibling,
            ancestors ?? Array.Empty<Guid>(),
            lines ?? new[] { Line(A, 10m), Line(B, 5m) });

    private static SalesReturnDraftEvidence Draft(
        IEnumerable<SalesReturnLineTerms>? lines = null,
        InvoiceSubjectSelection? subject = null, DateTime? issued = null,
        InvoiceTypePattern? pattern = null, Guid? owner = null,
        Guid? taxpayer = null, Guid? invoiceId = null, string buyer = "buyer-fingerprint") =>
        new(invoiceId ?? Guid.NewGuid(), owner ?? Owner, taxpayer ?? Taxpayer,
            issued ?? At.AddMinutes(1), pattern ??
            new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            subject ?? InvoiceSubjectSelection.Create(InvoiceSubject.SalesReturn,
                new InvoiceReference(TaxId, PredecessorId)), buyer,
            lines ?? new[] { Line(A, 8m), Line(B, 5m) });

    private static string[] Errors(SalesReturnDraftEvidence draft,
        SalesReturnPredecessor predecessor) =>
        OfficialV79SalesReturnRules.Evaluate(draft, predecessor)
            .Select(x => x.MessageKey).ToArray();

    [Fact]
    public void Partial_return_transmits_remaining_quantity_not_returned_quantity()
    {
        Assert.Empty(Errors(Draft(), Source())); // 10 -> 8, 5 -> 5
        Assert.Empty(Errors(Draft(lines: new[] { Line(A, 10m), Line(B, 2m) }), Source()));
    }

    [Fact]
    public void Unchanged_quantities_and_increases_are_rejected()
    {
        Assert.Contains("invoice.sales_return.quantity_not_reduced",
            Errors(Draft(lines: new[] { Line(A, 10m), Line(B, 5m) }), Source()));
        Assert.Contains("invoice.sales_return.quantity_increased",
            Errors(Draft(lines: new[] { Line(A, 11m), Line(B, 4m) }), Source()));
    }

    [Fact]
    public void Returning_entire_sale_requires_cancellation_not_sales_return()
    {
        Assert.Contains("invoice.sales_return.entire_sale_requires_cancellation",
            Errors(Draft(lines: new[] { Line(A, 0m), Line(B, 0m) }), Source()));
        Assert.Empty(Errors(Draft(lines: new[] { Line(B, 5m) }), Source()));
    }

    [Fact]
    public void Goods_identity_vat_and_commercial_terms_cannot_change()
    {
        Assert.Contains("invoice.sales_return.new_goods_service",
            Errors(Draft(lines: new[] { Line(A, 8m), Line("1111111111111", 1m), Line(B, 5m) }), Source()));
        Assert.Contains("invoice.sales_return.vat_rate_changed",
            Errors(Draft(lines: new[] { Line(A, 8m, 9m), Line(B, 5m) }), Source()));
        Assert.Contains("invoice.sales_return.commercial_terms_changed",
            Errors(Draft(lines: new[] { Line(A, 8m, price: 110m), Line(B, 5m) }), Source()));
        Assert.Contains("invoice.sales_return.commercial_terms_changed",
            Errors(Draft(lines: new[] { Line(A, 8m, discount: 6m), Line(B, 5m) }), Source()));
    }

    [Fact]
    public void Reference_fiscal_id_and_workspace_must_match()
    {
        Assert.Contains("invoice.sales_return.reference_mismatch", Errors(
            Draft(subject: InvoiceSubjectSelection.Create(InvoiceSubject.SalesReturn,
                new InvoiceReference("B123456789012345678901", PredecessorId))), Source()));
        Assert.Contains("invoice.sales_return.workspace_mismatch",
            Errors(Draft(), Source(owner: Guid.NewGuid())));
        Assert.Contains("invoice.sales_return.workspace_mismatch",
            Errors(Draft(), Source(taxpayer: Guid.NewGuid())));
    }

    [Fact]
    public void Unregistered_and_cancelled_predecessor_are_rejected()
    {
        Assert.Contains("invoice.sales_return.reference_inactive",
            Errors(Draft(), Source(registered: false)));
        Assert.Contains("invoice.sales_return.reference_inactive",
            Errors(Draft(), Source(cancelled: true)));
    }

    [Fact]
    public void Cyclic_links_and_earlier_date_are_rejected()
    {
        Assert.Contains("invoice.sales_return.reference_cycle",
            Errors(Draft(invoiceId: PredecessorId), Source()));
        var draft = Draft();
        Assert.Contains("invoice.sales_return.reference_cycle",
            Errors(draft, Source(ancestors: new[] { draft.InvoiceId })));
        Assert.Contains("invoice.sales_return.issue_before_reference",
            Errors(Draft(issued: At.AddMilliseconds(-1)), Source()));
    }

    [Fact]
    public void Correction_or_previous_return_requires_approved_reference()
    {
        foreach (var subject in new[] {
            SalesReturnPredecessorSubject.Correction,
            SalesReturnPredecessorSubject.SalesReturn
        })
        {
            Assert.Contains("invoice.sales_return.reference_response_ineligible",
                Errors(Draft(), Source(subject: subject,
                    response: SalesReturnPredecessorResponse.Pending)));
            Assert.Empty(Errors(Draft(), Source(subject: subject,
                response: SalesReturnPredecessorResponse.SystemApproved)));
        }
    }

    [Fact]
    public void Active_sibling_return_or_correction_is_rejected()
    {
        Assert.Contains("invoice.sales_return.active_related_conflict",
            Errors(Draft(), Source(activeSibling: true)));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(11)]
    public void Export_waybill_and_commodity_exchange_patterns_disallow_return(int pattern)
    {
        var classification = new InvoiceTypePattern(ElectronicInvoiceType.First, pattern);
        Assert.Contains("invoice.sales_return.pattern_not_eligible",
            Errors(Draft(pattern: classification), Source(pattern: classification)));
    }

    [Fact]
    public void Type_pattern_and_buyer_are_immutable_from_predecessor()
    {
        Assert.Contains("invoice.sales_return.classification_changed",
            Errors(Draft(pattern: new InvoiceTypePattern(ElectronicInvoiceType.First, 3)), Source()));
        Assert.Contains("invoice.sales_return.buyer_changed",
            Errors(Draft(buyer: "different-buyer"), Source()));
    }

    [Fact]
    public void Chained_returns_cannot_return_more_than_remaining()
    {
        var predecessor = Source(lines: new[] { Line(A, 4m), Line(B, 2m) },
            subject: SalesReturnPredecessorSubject.SalesReturn);
        Assert.Empty(Errors(Draft(lines: new[] { Line(A, 3m), Line(B, 2m) }), predecessor));
        Assert.Contains("invoice.sales_return.quantity_increased",
            Errors(Draft(lines: new[] { Line(A, 6m), Line(B, 1m) }), predecessor));
    }

    [Fact]
    public void Snapshots_are_read_only_and_findings_deterministic()
    {
        var prior = new List<SalesReturnLineTerms> { Line(A, 10m), Line(B, 5m) };
        var source = Source(lines: prior);
        prior.Clear();
        Assert.Equal(2, source.RemainingLines.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SalesReturnLineTerms>)source.RemainingLines).Clear());
        var d = Draft();
        Assert.Equal(OfficialV79SalesReturnRules.Evaluate(d, source),
            OfficialV79SalesReturnRules.Evaluate(d, source));
    }
}
