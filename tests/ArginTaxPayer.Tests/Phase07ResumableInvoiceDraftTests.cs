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
}
