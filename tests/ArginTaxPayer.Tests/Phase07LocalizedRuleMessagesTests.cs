using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07LocalizedRuleMessagesTests
{
    [Fact]
    public void Exact_fa_ir_and_en_us_messages_are_resolved_without_interpolation()
    {
        var messages = new InvoiceRuleMessageCatalog(new[]
        {
            ("invoice.money.vat_unresolved", "fa-IR", "نرخ مالیات مشخص نیست"),
            ("invoice.money.vat_unresolved", "en-US", "VAT rate is unresolved")
        });
        var finding = new InvoiceRuleFinding(
            new InvoiceRuleKey("invoice.money.tax_resolution"),
            InvoiceRuleSeverity.Error,
            "invoice.money.vat_unresolved", "body[0].vra");

        Assert.True(messages.TryResolve(finding, "fa-IR", out var persian));
        Assert.Equal("نرخ مالیات مشخص نیست", persian);
        Assert.True(messages.TryResolve(finding, "en-US", out var english));
        Assert.Equal("VAT rate is unresolved", english);
        Assert.False(messages.TryResolve(new InvoiceRuleFinding(
            new InvoiceRuleKey("invoice.money.tax_resolution"),
            InvoiceRuleSeverity.Error, "invoice.money.unknown"),
            "fa-IR", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void Duplicated_or_unknown_locales_fail_closed()
    {
        Assert.Throws<ArgumentException>(() => new InvoiceRuleMessageCatalog(new[]
        {
            ("invoice.error", "fa-IR", "خطا"),
            ("invoice.error", "fa-IR", "خطای تکراری")
        }));
        Assert.Throws<ArgumentException>(() => new InvoiceRuleMessageCatalog(new[]
        {
            ("invoice.error", "fr-FR", "Erreur")
        }));
    }
}
