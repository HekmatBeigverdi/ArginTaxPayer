using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07TypePatternRulesTests
{
    private static readonly DateTime Issued = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);
    private const string Reference = "A123456789012345678901";

    private static InvoiceRuleEvaluation Check(
        ElectronicInvoiceType type, int? pattern, InvoiceSubject subject,
        InvoicePatternHeaderFields? payload = null)
    {
        payload ??= subject == InvoiceSubject.Cancellation
            ? new InvoicePatternHeaderFields(null, null, Reference)
            : new InvoicePatternHeaderFields((int)type, pattern,
                subject == InvoiceSubject.Original ? null : Reference);
        var context = new InvoiceRuleContext(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Issued,
            new InvoiceTypePattern(type, pattern), subject, Array.Empty<Guid>(),
            OfficialV79TypePatternRuleFactory.Edition, patternHeader: payload);
        return OfficialV79TypePatternRuleFactory.Create().Evaluate(context);
    }

    [Fact]
    public void All_documented_V79_combinations_pass_classification()
    {
        foreach (var pattern in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 13, 14 })
        {
            var result = Check(ElectronicInvoiceType.First, pattern, InvoiceSubject.Original);
            Assert.DoesNotContain(result.Findings, f => f.RuleKey.Value == "invoice.pattern.combination");
        }
        foreach (var pattern in new[] { 1, 3, 9, 13 })
            Assert.DoesNotContain(Check(ElectronicInvoiceType.Second, pattern, InvoiceSubject.Original).Findings,
                f => f.RuleKey.Value == "invoice.pattern.combination");
        Assert.DoesNotContain(Check(ElectronicInvoiceType.Third, null, InvoiceSubject.Original).Findings,
            f => f.RuleKey.Value == "invoice.pattern.combination");
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(1, 12)]
    [InlineData(2, 2)]
    [InlineData(2, 7)]
    public void Undocumented_type_pattern_combination_is_rejected(int type, int pattern)
    {
        Assert.Contains(Check((ElectronicInvoiceType)type, pattern, InvoiceSubject.Original).Findings,
            f => f.MessageKey == "invoice.pattern.unsupported");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(11)]
    public void Export_waybill_commodity_exchange_disallow_sales_return(int pattern)
    {
        Assert.Contains(Check(ElectronicInvoiceType.First, pattern, InvoiceSubject.SalesReturn).Findings,
            f => f.MessageKey == "invoice.pattern.subject_disallowed");
        Assert.DoesNotContain(Check(ElectronicInvoiceType.First, pattern, InvoiceSubject.Correction).Findings,
            f => f.MessageKey == "invoice.pattern.subject_disallowed");
    }

    [Fact]
    public void Cancellation_must_omit_type_and_pattern_in_transmitted_header()
    {
        Assert.DoesNotContain(Check(ElectronicInvoiceType.First, 1, InvoiceSubject.Cancellation).Findings,
            f => f.RuleKey.Value == "invoice.pattern.payload");
        Assert.Contains(Check(ElectronicInvoiceType.First, 1, InvoiceSubject.Cancellation,
            new(1, 1, Reference)).Findings, f =>
            f.MessageKey == "invoice.pattern.cancellation_outside_pattern");
        Assert.Contains(Check(ElectronicInvoiceType.First, 1, InvoiceSubject.Correction,
            new(null, null, Reference)).Findings, f =>
            f.MessageKey == "invoice.pattern.type_pattern_mismatch");
    }

    [Fact]
    public void Only_referencing_subjects_can_transmit_reference_invoice_id()
    {
        Assert.Contains(Check(ElectronicInvoiceType.First, 1, InvoiceSubject.Original,
            new(1, 1, Reference)).Findings, f =>
            f.MessageKey == "invoice.pattern.reference_outside_pattern");
        Assert.Contains(Check(ElectronicInvoiceType.First, 1, InvoiceSubject.Correction,
            new(1, 1)).Findings, f =>
            f.MessageKey == "invoice.pattern.reference_required");
    }

    [Theory]
    [InlineData(4, "invoice.pattern.contract_required")]
    [InlineData(5, "invoice.pattern.bill_required")]
    [InlineData(6, "invoice.pattern.flight_required")]
    public void Dedicated_first_type_patterns_require_dedicated_fields(int pattern, string key)
    {
        Assert.Contains(Check(ElectronicInvoiceType.First, pattern, InvoiceSubject.Original).Findings,
            f => f.MessageKey == key);
    }

    [Fact]
    public void Required_dedicated_fields_accept_syntactically_valid_values()
    {
        Assert.DoesNotContain(Check(ElectronicInvoiceType.First, 4, InvoiceSubject.Original,
            new(1, 4, ContractNumber: "123456")).Findings, f =>
            f.MessageKey == "invoice.pattern.contract_required");
        Assert.DoesNotContain(Check(ElectronicInvoiceType.First, 5, InvoiceSubject.Original,
            new(1, 5, ServiceBillId: "123456789")).Findings, f =>
            f.MessageKey == "invoice.pattern.bill_required");
        Assert.DoesNotContain(Check(ElectronicInvoiceType.First, 6, InvoiceSubject.Original,
            new(1, 6, FlightType: 1)).Findings, f =>
            f.MessageKey == "invoice.pattern.flight_required");
    }

    [Fact]
    public void Gold_requires_the_four_documented_monetary_components()
    {
        Assert.Contains(Check(ElectronicInvoiceType.First, 3, InvoiceSubject.Original).Findings,
            f => f.MessageKey == "invoice.pattern.gold_fields_required");
        Assert.DoesNotContain(Check(ElectronicInvoiceType.Second, 3, InvoiceSubject.Original,
            new(2, 3, GoldConstructionFeeRial: 0m, GoldSellerProfitRial: 0m,
                GoldCommissionRial: 0m, GoldTotalFeeProfitRial: 0m)).Findings,
            f => f.MessageKey == "invoice.pattern.gold_fields_required");
    }

    [Fact]
    public void Special_fields_are_not_allowed_on_ordinary_original()
    {
        Assert.Contains(Check(ElectronicInvoiceType.First, 1, InvoiceSubject.Original,
            new(1, 1, ContractNumber: "12345", FlightType: 1)).Findings,
            f => f.MessageKey == "invoice.pattern.contract_outside_pattern");
        Assert.Contains(Check(ElectronicInvoiceType.First, 1, InvoiceSubject.Original,
            new(1, 1, ContractNumber: "12345", FlightType: 1)).Findings,
            f => f.MessageKey == "invoice.pattern.flight_outside_pattern");
    }

    [Fact]
    public void Missing_payload_is_rejected_and_result_never_authorizes_submission()
    {
        var ctx = new InvoiceRuleContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            Issued, new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            InvoiceSubject.Original, Array.Empty<Guid>(),
            OfficialV79TypePatternRuleFactory.Edition);
        var results = OfficialV79TypePatternRuleFactory.Create().Evaluate(ctx);
        Assert.True(results.HasErrors);
        Assert.False(results.IsSubmissionAuthorized);
    }

    [Fact]
    public void Deterministic_replay_produces_same_order_and_findings()
    {
        var payload = new InvoicePatternHeaderFields(1, 4, FlightType: 2);
        var one = Check(ElectronicInvoiceType.First, 4, InvoiceSubject.Original, payload);
        var two = Check(ElectronicInvoiceType.First, 4, InvoiceSubject.Original, payload);
        Assert.Equal(one.Findings, two.Findings);
    }
}
