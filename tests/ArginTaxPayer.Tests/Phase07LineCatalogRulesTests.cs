using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07LineCatalogRulesTests
{
    private static readonly DateTime Issued = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Captured = Issued.AddMinutes(-5);
    private static InvoiceCatalogHandoff Handoff(Guid owner) => new(
        1, owner, Guid.NewGuid(), 3, "SKU-8", "کالای داخلی",
        CatalogItemKind.StockItem, Guid.NewGuid(), "1627", "عدد",
        Guid.NewGuid(), "RC_UMGS.ST", "V1.18", new string('a', 64),
        Guid.NewGuid(), 2, Issued.AddDays(-2), null,
        Guid.NewGuid(), "1234567890123", "عنوان رسمی",
        Guid.NewGuid(), "RC_GS", "2026", new string('b', 64),
        Issued, Captured, InvoiceVatResolution.Unresolved);

    private static InvoiceLineSnapshot Line(Guid owner, int position, decimal quantity = 1m,
        InvoiceCatalogHandoff? handoff = null) =>
        InvoiceLineSnapshot.Create(Guid.NewGuid(), position, quantity,
            handoff ?? Handoff(owner), owner);

    private static InvoiceRuleEvaluation Check(
        IReadOnlyList<InvoiceLineSnapshot>? lines,
        Guid owner, int pattern = 1, InvoiceSubject subject = InvoiceSubject.Original,
        IEnumerable<Guid>? lineIds = null, DateTime? time = null) =>
        OfficialV79LineCatalogRuleFactory.Create().Evaluate(new InvoiceRuleContext(
            Guid.NewGuid(), owner, Guid.NewGuid(), 1,
            time ?? Issued, new InvoiceTypePattern(ElectronicInvoiceType.First, pattern),
            subject, lineIds ?? lines?.Select(l => l.Id) ?? Array.Empty<Guid>(),
            OfficialV79LineCatalogRuleFactory.Edition,
            asOfUtc: Issued, catalogLines: lines));

    [Fact]
    public void Valid_phase06_snapshot_is_structurally_consistent_but_never_authorized()
    {
        var owner = Guid.NewGuid();
        var result = Check(new[] { Line(owner, 1, 2.5m) }, owner);
        Assert.Empty(result.Findings);
        Assert.False(result.IsSubmissionAuthorized);
    }

    [Fact]
    public void Missing_empty_and_mismatched_line_snapshots_fail_closed()
    {
        var owner = Guid.NewGuid();
        Assert.Contains(Check(null, owner).Findings,
            f => f.MessageKey == "invoice.lines.snapshot_missing");
        Assert.Contains(Check(Array.Empty<InvoiceLineSnapshot>(), owner).Findings,
            f => f.MessageKey == "invoice.lines.empty");
        var line = Line(owner, 1);
        Assert.Contains(Check(new[] { line }, owner, lineIds: new[] { Guid.NewGuid() }).Findings,
            f => f.MessageKey == "invoice.lines.snapshot_mismatch");
    }

    [Fact]
    public void Tenant_mismatch_is_rejected_even_with_complete_official_catalog_provenance()
    {
        var owner = Guid.NewGuid();
        var alien = Guid.NewGuid();
        var line = Line(alien, 1);
        Assert.Contains(Check(new[] { line }, owner).Findings,
            f => f.MessageKey == "invoice.lines.incomplete_or_foreign_handoff");
    }

    [Fact]
    public void Official_code_must_be_thirteen_ascii_digits()
    {
        var owner = Guid.NewGuid();
        var snapshot = Handoff(owner) with { OfficialCode = "۱۲۳۴۵۶۷۸۹۰۱۲۳" };
        Assert.Contains(Check(new[] { Line(owner, 1, handoff: snapshot) }, owner).Findings,
            f => f.MessageKey == "invoice.lines.goods_service_code" && f.FieldPath == "body[0].sstid");
        snapshot = Handoff(owner) with { OfficialCode = "123" };
        Assert.Contains(Check(new[] { Line(owner, 1, handoff: snapshot) }, owner).Findings,
            f => f.MessageKey == "invoice.lines.goods_service_code");
    }

    [Fact]
    public void Mapping_must_be_effective_at_actual_invoice_issue_time()
    {
        var owner = Guid.NewGuid();
        var snapshot = Handoff(owner) with
        {
            MappingEffectiveFromUtc = Issued.AddDays(-2),
            MappingEffectiveToUtc = Issued.AddHours(-1),
            EffectiveAtUtc = Issued.AddDays(-1)
        };
        var line = Line(owner, 1, handoff: snapshot);
        Assert.Contains(Check(new[] { line }, owner).Findings,
            f => f.MessageKey == "invoice.lines.mapping_not_effective");
    }

    [Fact]
    public void Source_sha_and_release_identity_are_checked()
    {
        var owner = Guid.NewGuid();
        var snap = Handoff(owner) with { UnitReleaseSha256 = new string('z', 64) };
        var line = Line(owner, 1, handoff: snap);
        Assert.Contains(Check(new[] { line }, owner).Findings,
            f => f.MessageKey == "invoice.lines.incomplete_or_foreign_handoff");
        var bad = Handoff(owner) with { UnitReleaseDocument = "unverified" };
        Assert.Contains(Check(new[] { Line(owner, 1, handoff: bad) }, owner).Findings,
            f => f.MessageKey == "invoice.lines.unit_release_unverified");
    }

    [Theory]
    [InlineData(1.123456789)]
    [InlineData(1000000000000000000d)]
    public void Quantity_beyond_official_precision_or_integer_bound_is_rejected(double amount)
    {
        var owner = Guid.NewGuid();
        var line = Line(owner, 1, (decimal)amount);
        Assert.Contains(Check(new[] { line }, owner).Findings,
            f => f.MessageKey == "invoice.lines.quantity_invalid");
    }

    [Fact]
    public void Exactly_eight_decimal_places_are_permitted()
    {
        var owner = Guid.NewGuid();
        Assert.DoesNotContain(Check(new[] { Line(owner, 1, 1.12345678m) }, owner).Findings,
            f => f.RuleKey.Value == "invoice.lines.quantity");
    }

    [Fact]
    public void Unit_must_be_ascii_numeric_and_refer_to_reviewed_unit_release()
    {
        var owner = Guid.NewGuid();
        var snap = Handoff(owner) with { OfficialUnitCode = "ABCDEFGHI" };
        Assert.Contains(Check(new[] { Line(owner, 1, handoff: snap) }, owner).Findings,
            f => f.MessageKey == "invoice.lines.official_unit_invalid");
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public void Flight_and_waybill_quantities_must_be_exactly_one(int pattern)
    {
        var owner = Guid.NewGuid();
        Assert.Contains(Check(new[] { Line(owner, 1, 2m) }, owner, pattern).Findings,
            f => f.MessageKey == "invoice.lines.quantity_one_required");
        Assert.DoesNotContain(Check(new[] { Line(owner, 1) }, owner, pattern).Findings,
            f => f.MessageKey == "invoice.lines.quantity_one_required");
    }

    [Theory]
    [InlineData(11)]
    [InlineData(14)]
    public void Commodity_securities_and_chain_sales_have_only_one_line(int pattern)
    {
        var owner = Guid.NewGuid();
        var result = Check(new[] { Line(owner, 1), Line(owner, 2) }, owner, pattern);
        Assert.Contains(result.Findings,
            f => f.MessageKey == "invoice.lines.single_line_required");
    }

    [Fact]
    public void Pure_evaluation_does_not_mutate_catalog_and_is_deterministic()
    {
        var owner = Guid.NewGuid();
        var source = Handoff(owner);
        var item = Line(owner, 1, handoff: source);
        var lines = new List<InvoiceLineSnapshot> { item };
        var context = new InvoiceRuleContext(Guid.NewGuid(), owner, Guid.NewGuid(), 1,
            Issued, new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            InvoiceSubject.Original, new[] { item.Id },
            OfficialV79LineCatalogRuleFactory.Edition, asOfUtc: Issued,
            catalogLines: lines);
        lines.Clear();
        var engine = OfficialV79LineCatalogRuleFactory.Create();
        Assert.Equal(engine.Evaluate(context).Findings, engine.Evaluate(context).Findings);
        Assert.Equal(source, Assert.Single(context.CatalogLines!).Catalog);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<InvoiceLineSnapshot>)context.CatalogLines!).Clear());
    }
}
