using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10InvoiceWireMapperTests
{
    private static InvoiceWireInput Make(ElectronicInvoiceType type = ElectronicInvoiceType.First,
        int pattern = 1, InvoiceSubject subject = InvoiceSubject.Original,
        string? crn = null, string? reference = null)
    {
        var lines = new[] { new InvoiceWireLine("1234567890123", 2m, 100m, 200, 20,
            180, 10m, 18, 198, "خدمات", "1") };
        return new(new InvoiceTypePattern(type, pattern), subject,
            "AAAAAAAAAAAAAAAAAAAAAA", 1790000000000, "1234567890", lines,
            200, 20, 180, 18, 198, ContractRegistrationNumber: crn,
            ReferenceTaxId: reference);
    }

    [Theory]
    [InlineData(ElectronicInvoiceType.First, 1)]
    [InlineData(ElectronicInvoiceType.Second, 1)]
    public void General_invoice_golden_field_projection(ElectronicInvoiceType type, int pattern)
    {
        var result = Phase10InvoiceWireMapper.Project(Make(type, pattern));
        using var doc = JsonDocument.Parse(result.Json);
        var h = doc.RootElement.GetProperty("header");
        Assert.Equal((int)type, h.GetProperty("inty").GetInt32());
        Assert.Equal(1, h.GetProperty("inp").GetInt32());
        Assert.False(h.TryGetProperty("crn", out _));
        var b = doc.RootElement.GetProperty("body")[0];
        Assert.Equal("1234567890123", b.GetProperty("sstid").GetString());
        Assert.Equal(180, b.GetProperty("adis").GetInt64());
        Assert.Equal(198, h.GetProperty("tbill").GetInt64());
        Assert.False(result.IsSubmissionAuthorized);
        Assert.Equal(result.Json, Phase10InvoiceWireMapper.Project(Make(type, pattern)).Json);
    }

    [Fact]
    public void Contracting_has_registered_crn_and_same_shared_transport_shape()
    {
        var result = Phase10InvoiceWireMapper.Project(Make(pattern: 4, crn: "123456"));
        using var doc = JsonDocument.Parse(result.Json);
        Assert.Equal("123456", doc.RootElement.GetProperty("header").GetProperty("crn").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("body").GetArrayLength());
    }

    [Fact]
    public void Special_patterns_and_type2_contracting_fail_closed()
    {
        Assert.Throws<NotSupportedException>(() => Phase10InvoiceWireMapper.Project(Make(pattern: 3)));
        Assert.Throws<NotSupportedException>(() => Phase10InvoiceWireMapper.Project(
            Make(ElectronicInvoiceType.Second, 4, crn: "1234")));
        Assert.Throws<ArgumentException>(() => Phase10InvoiceWireMapper.Project(Make(pattern: 4)));
        Assert.Throws<ArgumentException>(() => Phase10InvoiceWireMapper.Project(Make(crn: "1234")));
    }

    [Fact]
    public void Referring_subject_projects_reference_and_omits_cancellation_type_fields()
    {
        const string reference = "BBBBBBBBBBBBBBBBBBBBBB";
        var correction = Phase10InvoiceWireMapper.Project(
            Make(subject: InvoiceSubject.Correction, reference: reference));
        using var c = JsonDocument.Parse(correction.Json);
        Assert.Equal(reference, c.RootElement.GetProperty("header").GetProperty("irtaxid").GetString());

        var cancellation = Make(subject: InvoiceSubject.Cancellation, reference: reference)
            with { Lines = Array.Empty<InvoiceWireLine>() };
        var result = Phase10InvoiceWireMapper.Project(cancellation);
        using var doc = JsonDocument.Parse(result.Json);
        var h = doc.RootElement.GetProperty("header");
        Assert.False(h.TryGetProperty("inty", out _));
        Assert.False(h.TryGetProperty("inp", out _));
        Assert.Equal(reference, h.GetProperty("irtaxid").GetString());
        Assert.Throws<ArgumentException>(() => Phase10InvoiceWireMapper.Project(
            Make(subject: InvoiceSubject.SalesReturn)));
    }

    [Fact]
    public void Mismatched_financial_totals_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Phase10InvoiceWireMapper.Project(
            Make() with { TotalBillRial = 199 }));
        Assert.Throws<ArgumentException>(() => Phase10InvoiceWireMapper.Project(
            Make() with { TotalVatRial = 17 }));
    }

    [Fact]
    public void Payment_and_settlement_unknown_rules_fail_closed()
    {
        var cash = Phase10InvoiceWireMapper.Project(Make() with {
            SettlementMethod = 1, CashAmountRial = 198, CreditAmountRial = 0,
            InvoiceNumber = "00010-05-AAA"
        });
        using (var doc = JsonDocument.Parse(cash.Json))
        {
            var header = doc.RootElement.GetProperty("header");
            Assert.Equal(1, header.GetProperty("setm").GetInt32());
            Assert.Equal(198, header.GetProperty("cap").GetInt64());
            Assert.Equal(0, header.GetProperty("insp").GetInt64());
            Assert.Equal("00010-05-AAA", header.GetProperty("inno").GetString());
        }
        Assert.Throws<ArgumentException>(() => Phase10InvoiceWireMapper.Project(
            Make() with { SettlementMethod = 1, CashAmountRial = 197, CreditAmountRial = 1 }));
        Assert.Throws<NotSupportedException>(() => Phase10InvoiceWireMapper.Project(
            Make() with { Payments = new[] { new InvoiceWirePayment(null, null, 198) } }));
    }
}
