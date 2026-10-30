using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07ResumableInvoiceDraftTests
{
    private static ArginTaxPayerDbContext Context() => new(
        new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=argintaxpayer_test;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Draft_migration_is_explicit_and_discoverable_without_live_database()
    {
        using var db = Context();
        Assert.Contains("20261018150000_AddResumableInvoiceEntryWorkspaces",
            db.Database.GetMigrations());
    }

    [Fact]
    public void Entry_draft_tracks_owner_taxpayer_and_optimistic_revision()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(InvoiceEntryWorkspace))!;
        Assert.Equal("invoice_entry_workspaces",entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(InvoiceEntryWorkspace.Revision))!.IsConcurrencyToken);
        Assert.Equal("jsonb",entity.FindProperty(nameof(InvoiceEntryWorkspace.DraftJson))!.GetColumnType());
        Assert.Contains(entity.GetForeignKeys(),x => x.Properties.Single().Name == "TaxpayerId");
        Assert.Contains(entity.GetForeignKeys(),x => x.Properties.Single().Name == "CustomerUserId");
    }

    [Fact]
    public void Editable_draft_has_no_send_or_finalize_method()
    {
        var type = typeof(InvoiceEntryWorkspaceService);
        Assert.NotNull(type.GetMethod("SaveAsync"));
        Assert.NotNull(type.GetMethod("GetAsync"));
        Assert.NotNull(type.GetMethod("ListAsync"));
        Assert.Null(type.GetMethod("SubmitAsync"));
        Assert.Null(type.GetMethod("FinalizeAsync"));
    }

    [Fact]
    public void Saved_draft_contract_can_resume_without_buyer()
    {
        var request = new SaveInvoiceEntryRequest(null,Guid.NewGuid(),0,0,
            DateTime.UtcNow,null,1,1,[]);
        Assert.Null(request.BuyerPartyId);
        Assert.Equal(0,request.LastCompletedStep);
    }
    [Fact]
    public void Description_and_settlement_contracts_are_optional_in_incomplete_drafts()
    {
        var request = new SaveInvoiceEntryRequest(null,Guid.NewGuid(),0,2,DateTime.UtcNow,
            null,1,1,[],InvoiceDescription:"توضیحات داخلی",SettlementMethod:InvoiceDraftSettlementMethod.Unknown);
        Assert.Equal("توضیحات داخلی", request.InvoiceDescription);
        Assert.Null(request.CashAmountRial);
        Assert.Null(request.CreditAmountRial);
        Assert.Equal(InvoiceDraftSettlementMethod.Unknown,request.SettlementMethod);
    }

    [Fact]
    public void Preview_totals_include_discount_and_vat_from_lines()
    {
        var summary=InvoiceDraftSettlementPolicy.Calculate([
            new InvoiceEntryLine(null,"کالا",2m,100m,10m,10m)
        ]);
        Assert.Equal(200m,summary.GrossRial);
        Assert.Equal(20m,summary.DiscountRial);
        Assert.Equal(180m,summary.AfterDiscountRial);
        Assert.Equal(18m,summary.PreviewVatRial);
        Assert.Equal(198m,summary.PreviewGrandTotalRial);
    }

    [Fact]
    public void Promotion_settlement_total_includes_other_tax_and_legal_fees()
    {
        var consistent = InvoiceDraftSettlementPolicy.IsConsistent(
            InvoiceDraftSettlementMethod.Cash, 200m, 0m,
            [new InvoiceEntryLine(null,"کالا",1m,180m,0m,0m)], 20m);

        Assert.True(consistent);
    }

    [Theory]
    [InlineData(InvoiceDraftSettlementMethod.Unknown,null,null,true)]
    [InlineData(InvoiceDraftSettlementMethod.Unknown,0,0,false)]
    [InlineData(InvoiceDraftSettlementMethod.Cash,198,0,true)]
    [InlineData(InvoiceDraftSettlementMethod.Credit,0,198,true)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit,100,98,true)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit,198,0,false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit,200,-2,false)]
    [InlineData(InvoiceDraftSettlementMethod.CashAndCredit,100,100,false)]
    [InlineData(InvoiceDraftSettlementMethod.Cash,0,198,false)]
    public void Settlement_validation_checks_method_and_balance(
        InvoiceDraftSettlementMethod method,int? cash,int? credit,bool expected)
    {
        Assert.Equal(expected,InvoiceDraftSettlementPolicy.IsConsistent(method,
            cash,credit,198m));
    }

    [Fact]
    public void Resumable_draft_view_exposes_description_and_settlement_fields()
    {
        var names=typeof(InvoiceEntryView).GetProperties().Select(p=>p.Name).ToArray();
        Assert.Contains("InvoiceDescription",names);
        Assert.Contains("SettlementMethod",names);
        Assert.Contains("CashAmountRial",names);
        Assert.Contains("CreditAmountRial",names);
        Assert.Contains("FinancialSummary",names);
    }

}
