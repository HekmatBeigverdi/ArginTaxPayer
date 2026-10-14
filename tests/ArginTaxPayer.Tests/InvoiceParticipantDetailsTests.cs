using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class InvoiceParticipantDetailsTests
{
    [Fact]
    public void Buyers_and_sellers_are_separately_scoped_and_revision_guarded()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=not_connected;Username=never;Password=never").Options);
        var type = db.Model.FindEntityType(typeof(InvoiceParticipantDetails))!;
        Assert.Equal("invoice_participant_details", type.GetTableName());
        Assert.True(type.FindProperty(nameof(InvoiceParticipantDetails.Revision))!.IsConcurrencyToken);
        Assert.Equal(3, type.GetForeignKeys().Count());
        Assert.All(type.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(type.GetIndexes(), i => i.IsUnique &&
            i.GetFilter() == "party_id IS NOT NULL");
        Assert.Contains(type.GetIndexes(), i => i.IsUnique &&
            i.GetFilter() == "taxpayer_id IS NOT NULL");
    }
}
