using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Phase07 Step22: deterministic cross-feature regressions.
/// No database or tax-authority connection, credentials or mutable fixtures.
/// Live PostgreSQL/HTTP acceptance is handled by the separate read-only smoke.
/// </summary>
public sealed class Phase07ComprehensiveRegressionTests
{
    private static readonly InvoiceTypePattern Standard =
        new(ElectronicInvoiceType.First, 1);

    [Theory]
    [InlineData(ElectronicInvoiceType.First, 1, InvoiceSubject.Original, true)]
    [InlineData(ElectronicInvoiceType.First, 1, InvoiceSubject.Correction, true)]
    [InlineData(ElectronicInvoiceType.First, 1, InvoiceSubject.Cancellation, true)]
    [InlineData(ElectronicInvoiceType.First, 1, InvoiceSubject.SalesReturn, true)]
    [InlineData(ElectronicInvoiceType.First, 7, InvoiceSubject.SalesReturn, false)]
    [InlineData(ElectronicInvoiceType.First, 8, InvoiceSubject.SalesReturn, false)]
    [InlineData(ElectronicInvoiceType.First, 11, InvoiceSubject.SalesReturn, false)]
    [InlineData(ElectronicInvoiceType.Second, 1, InvoiceSubject.Original, true)]
    public void Official_subject_pattern_matrix_regression(
        ElectronicInvoiceType type, int pattern, InvoiceSubject subject, bool permitted)
    {
        Assert.Equal(permitted, OfficialV79InvoiceSubjectPolicy.Allows(
            new InvoiceTypePattern(type, pattern), subject));
    }

    [Theory]
    [InlineData(-1, 0, 0, false)]
    [InlineData(0, -1, 0, false)]
    [InlineData(0, 0, -1, false)]
    [InlineData(100, 0, 0, true)]
    [InlineData(0, 0, 0, true)]
    public void Draft_settlement_cannot_accept_negative_amounts(
        int grand, int cash, int credit, bool expected)
    {
        // Unknown settlement is not proof of cash received.
        Assert.Equal(expected, InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.Unknown, cash, credit, grand));
    }

    [Theory]
    [InlineData(InvoiceDraftSettlementMethod.Cash, 198, 0, true)]
    [InlineData(InvoiceDraftSettlementMethod.Credit, 0, 198, true)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, 100, 98, true)]
    [InlineData(InvoiceDraftSettlementMethod.Cash, 100, 98, false)]
    [InlineData(InvoiceDraftSettlementMethod.Credit, 100, 98, false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, 198, 0, false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, 0, 198, false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit, 100, 99, false)]
    public void Settlement_method_and_total_must_agree(
        InvoiceDraftSettlementMethod method, int cash, int credit, bool permitted)
    {
        Assert.Equal(permitted, InvoiceDraftSettlementPolicy.IsConsistent(
            method, cash, credit, 198m));
    }

    [Fact]
    public void Draft_preview_financials_are_not_an_official_issued_invoice()
    {
        var lines = new[]
        {
            new InvoiceEntryLine(null, "کالای نمونه", 2m, 100m, 10m, 10m),
            new InvoiceEntryLine(null, "خدمت نمونه", 1m, 200m, 0m, 0m)
        };
        var preview = InvoiceDraftSettlementPolicy.Calculate(lines);
        Assert.Equal(400m, preview.GrossRial);
        Assert.Equal(20m, preview.DiscountRial);
        Assert.Equal(380m, preview.AfterDiscountRial);
        Assert.Equal(18m, preview.PreviewVatRial);
        Assert.Equal(398m, preview.PreviewGrandTotalRial);

        // The official calculator is intentionally distinct from this
        // work-in-progress editable preview and has separate precision rules.
        Assert.Equal(OfficialV79InvoicePatternMatrix.Edition,
            new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition).Edition);
        Assert.Equal(1m, new InvoiceRoundingPolicy(
            OfficialV79InvoicePatternMatrix.Edition).Rial(1.999m));
    }

    [Fact]
    public void Older_json_workspaces_keep_compatible_optional_settlement_defaults()
    {
        const string legacy = """
        {
          "Id":null,"TaxpayerId":"11111111-1111-1111-1111-111111111111",
          "ExpectedRevision":0,"LastCompletedStep":1,
          "IssueDateUtc":"2026-10-18T09:00:00Z",
          "BuyerPartyId":null,"Type":1,"Pattern":1,"Lines":[]
        }
        """;
        var value = JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(legacy);
        Assert.NotNull(value);
        Assert.Equal(InvoiceDraftSettlementMethod.Unknown, value.SettlementMethod);
        Assert.Null(value.InvoiceDescription);
        Assert.Null(value.CashAmountRial);
        Assert.Null(value.CreditAmountRial);
        Assert.Null(value.BuyerPartyId);
        Assert.Empty(value.Lines!);
    }

    [Fact]
    public void Multi_step_json_roundtrip_preserves_draft_revision_and_information()
    {
        var id = Guid.NewGuid();
        var request = new SaveInvoiceEntryRequest(
            id, Guid.NewGuid(), 7, 2,
            new DateTime(2026,10,18,9,0,0,DateTimeKind.Utc),
            Guid.NewGuid(), 1, 1,
            [new InvoiceEntryLine(null,"شرح آزمایشی",2m,100m,10m,10m)],
            BuyerDisplayName:"خریدار آزمایشی",
            InvoiceDescription:"توضیح داخلی\nخط دوم",
            SettlementMethod:InvoiceDraftSettlementMethod.CashAndCredit,
            CashAmountRial:100m,CreditAmountRial:98m);
        var restored = JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(
            JsonSerializer.Serialize(request));
        Assert.NotNull(restored);
        Assert.Equal(id,restored.Id);
        Assert.Equal(7,restored.ExpectedRevision);
        Assert.Equal(2,restored.LastCompletedStep);
        Assert.Equal(request.BuyerPartyId,restored.BuyerPartyId);
        Assert.Equal("توضیح داخلی\nخط دوم",restored.InvoiceDescription);
        Assert.Equal(InvoiceDraftSettlementMethod.CashAndCredit,restored.SettlementMethod);
        Assert.Equal(100m,restored.CashAmountRial);
        Assert.Equal(98m,restored.CreditAmountRial);
        Assert.Single(restored.Lines!);
    }

    [Fact]
    public void Reference_projections_never_fabricate_fiscal_tax_ids()
    {
        var reference = new InvoiceUiReference(Guid.NewGuid(),
            "Original","Draft",DateTime.UtcNow,2,false,null);
        Assert.False(reference.FiscalIdentifierVerified);
        Assert.Null(reference.FiscalIdentifier);
        var evaluation = new InvoiceUiValidation(Guid.NewGuid(),2,
            OfficialV79InvoicePatternMatrix.Edition,false,[]);
        Assert.False(evaluation.IsSubmissionAuthorized);
    }

    [Fact]
    public void Draft_service_does_not_expose_authority_mutations()
    {
        var entry = typeof(InvoiceEntryWorkspaceService);
        var validation = typeof(InvoiceValidationReferenceService);
        var methods = new[] {"SubmitAsync","SendAsync","FinalizeAsync",
            "AcceptAsync","ApproveReferenceAsync"};
        foreach(var method in methods)
        {
            Assert.Null(entry.GetMethod(method));
            Assert.Null(validation.GetMethod(method));
        }
        Assert.NotNull(entry.GetMethod("SaveAsync"));
        Assert.NotNull(entry.GetMethod("GetAsync"));
        Assert.NotNull(validation.GetMethod("ValidateAsync"));
        Assert.NotNull(validation.GetMethod("ListReferencesAsync"));
    }

    [Fact]
    public void Original_and_referenced_invoice_contracts_cannot_be_confused()
    {
        Assert.Null(InvoiceSubjectSelection.Original().Reference);
        Assert.Throws<ArgumentException>(()=>
            InvoiceSubjectSelection.Create(InvoiceSubject.Correction));
        Assert.Throws<ArgumentException>(()=>
            InvoiceSubjectSelection.Create(InvoiceSubject.Original,
                new InvoiceReference("A123456789012345678901")));
        Assert.Throws<ArgumentException>(()=>
            new InvoiceReference("INVALID"));
    }

    [Fact]
    public void Empty_legacy_draft_is_valid_for_recovery_but_not_submission()
    {
        var entry = new SaveInvoiceEntryRequest(null,Guid.NewGuid(),0,0,
            DateTime.UtcNow,null,1,1,[]);
        Assert.Empty(entry.Lines!);
        Assert.Null(entry.BuyerPartyId);
        Assert.True(InvoiceDraftSettlementPolicy.IsConsistent(
            entry.SettlementMethod,entry.CashAmountRial,entry.CreditAmountRial,0m));
        Assert.False(new InvoiceUiValidation(Guid.NewGuid(),1,
            OfficialV79InvoicePatternMatrix.Edition,false,[]).IsSubmissionAuthorized);
    }
}
