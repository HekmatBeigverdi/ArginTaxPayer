using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07HeaderValidationTests
{
    private static readonly DateTime Issued = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);
    private const string TaxId = "A123456789012345678901";

    private static InvoiceRuleEvaluation Check(InvoiceHeaderFields? header,
        ElectronicInvoiceType type = ElectronicInvoiceType.First, int? pattern = 1,
        InvoiceSubject subject = InvoiceSubject.Original, DateTime? asOf = null)
    {
        var ctx = new InvoiceRuleContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            1, Issued, new InvoiceTypePattern(type, type == ElectronicInvoiceType.Third ? null : pattern),
            subject, new[] { Guid.NewGuid() }, InvoiceHeaderRuleFactory.Edition, header, asOf ?? Issued);
        return InvoiceHeaderRuleFactory.Create().Evaluate(ctx);
    }

    private static InvoiceHeaderFields Valid() => new(
        TaxId, new DateTimeOffset(Issued).ToUnixTimeMilliseconds(), "12345678901",
        2, "12345678901");

    [Fact]
    public void Valid_ordinary_legal_buyer_header_has_no_structural_errors()
    {
        var result = Check(Valid());
        Assert.False(result.HasErrors);
        Assert.Empty(result.Findings);
        Assert.False(result.IsSubmissionAuthorized);
    }

    [Fact]
    public void Missing_and_invalid_taxid_are_reported_at_official_field()
    {
        var missing = Check(Valid() with { TaxId = null });
        Assert.Contains(missing.Findings, f => f.MessageKey == "invoice.header.taxid.required"
            && f.FieldPath == "header.taxid");
        var invalid = Check(Valid() with { TaxId = "A123" });
        Assert.Contains(invalid.Findings, f => f.MessageKey == "invoice.header.taxid.invalid_structure");
    }

    [Fact]
    public void Unix_milliseconds_must_equal_issue_timestamp_and_not_be_future()
    {
        var valid = Valid();
        Assert.Contains(Check(valid with { IssuedUnixMilliseconds = null }).Findings,
            f => f.MessageKey == "invoice.header.issue_date.required");
        Assert.Contains(Check(valid with { IssuedUnixMilliseconds = -1 }).Findings,
            f => f.MessageKey == "invoice.header.issue_date.invalid");
        Assert.Contains(Check(valid with { IssuedUnixMilliseconds = valid.IssuedUnixMilliseconds + 1000 }).Findings,
            f => f.MessageKey == "invoice.header.issue_date.invalid");
        Assert.Contains(Check(valid, asOf: Issued.AddMilliseconds(-1)).Findings,
            f => f.MessageKey == "invoice.header.issue_date.future");
    }

    [Fact]
    public void Seller_economic_id_is_required_and_must_have_11_or_14_digits()
    {
        Assert.Contains(Check(Valid() with { SellerEconomicId = null }).Findings,
            f => f.FieldPath == "header.tins");
        Assert.Contains(Check(Valid() with { SellerEconomicId = "۱۲۳۴۵۶۷۸۹۰۱" }).Findings,
            f => f.MessageKey == "invoice.header.seller.economic_id");
        Assert.DoesNotContain(Check(Valid() with { SellerEconomicId = "12345678901234" }).Findings,
            f => f.MessageKey == "invoice.header.seller.economic_id");
    }

    [Fact]
    public void Buyer_type_one_natural_requires_economic_or_identity_and_postal()
    {
        var initial = Valid() with { BuyerPersonType = 1, BuyerEconomicId = null };
        Assert.Contains(Check(initial).Findings,
            f => f.MessageKey == "invoice.header.buyer.natural_identity_required");
        var complete = initial with { BuyerIdentityId = "1234567890", BuyerPostalCode = "1234567890" };
        Assert.DoesNotContain(Check(complete).Findings,
            f => f.RuleKey.Value == "invoice.header.buyer");
    }

    [Fact]
    public void Legal_buyer_needs_economic_id_on_type_one()
    {
        Assert.Contains(Check(Valid() with { BuyerEconomicId = null }).Findings,
            f => f.MessageKey == "invoice.header.buyer.economic_required");
    }

    [Fact]
    public void Type_two_may_omit_buyer_information()
    {
        var h = new InvoiceHeaderFields(TaxId, new DateTimeOffset(Issued).ToUnixTimeMilliseconds(), "12345678901");
        Assert.Empty(Check(h, type: ElectronicInvoiceType.Second).Findings);
    }

    [Fact]
    public void Correction_cancellation_and_return_must_omit_buyer_fields()
    {
        foreach (var subject in new[] { InvoiceSubject.Correction, InvoiceSubject.Cancellation, InvoiceSubject.SalesReturn })
        {
            Assert.Contains(Check(Valid(), subject: subject).Findings,
                f => f.MessageKey == "invoice.header.buyer.outside_pattern");
            var h = new InvoiceHeaderFields(TaxId, new DateTimeOffset(Issued).ToUnixTimeMilliseconds(), "12345678901");
            Assert.Empty(Check(h, subject: subject).Findings);
        }
    }

    [Fact]
    public void Buyer_optional_for_export_and_commodity_exchanges()
    {
        var h = new InvoiceHeaderFields(TaxId, new DateTimeOffset(Issued).ToUnixTimeMilliseconds(), "12345678901");
        Assert.Empty(Check(h, pattern: 7).Findings);
        Assert.Empty(Check(h, pattern: 11).Findings);
    }

    [Fact]
    public void Missing_header_is_not_considered_valid()
    {
        var findings = Check(null).Findings;
        Assert.Contains(findings, x => x.RuleKey.Value == "invoice.header.taxid");
        Assert.Contains(findings, x => x.RuleKey.Value == "invoice.header.issue_date");
        Assert.Contains(findings, x => x.RuleKey.Value == "invoice.header.seller");
        Assert.Contains(findings, x => x.RuleKey.Value == "invoice.header.buyer");
    }
}
