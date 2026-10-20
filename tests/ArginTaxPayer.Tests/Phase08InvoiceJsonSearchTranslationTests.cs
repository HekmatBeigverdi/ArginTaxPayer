using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08InvoiceJsonSearchTranslationTests
{
    [Fact]
    public void JsonBuyerNameExtractionTranslatesToPostgresFunction()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=none;Password=none")
            .Options;
        using var db = new ArginTaxPayerDbContext(options);
        var statement = db.InvoiceEntryWorkspaces.Where(w =>
            EF.Functions.ILike(InvoiceJsonFields.Text(w.DraftJson, "BuyerDisplayName")!, "%buyer%"))
            .ToQueryString();
        Assert.Contains("jsonb_extract_path_text", statement);
        Assert.Contains("ILIKE", statement);
    }

    [Fact]
    public void BuyerPartyIdentifierExtractionTranslatesToSql()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=none;Password=none")
            .Options;
        using var db = new ArginTaxPayerDbContext(options);
        var statement = db.InvoiceEntryWorkspaces.Where(w =>
            db.Parties.Any(p => InvoiceJsonFields.Text(w.DraftJson, "BuyerPartyId") == p.Id.ToString()))
            .ToQueryString();
        Assert.Contains("jsonb_extract_path_text", statement);
    }
}
