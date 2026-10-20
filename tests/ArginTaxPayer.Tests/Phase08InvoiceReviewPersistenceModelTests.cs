using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08InvoiceReviewPersistenceModelTests
{
    [Fact]
    public void Review_and_event_entities_are_mapped_to_separate_tables_with_revision_and_scope()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=test;Password=test")
            .Options;
        using var db = new ArginTaxPayerDbContext(options);
        var review = db.Model.FindEntityType(typeof(StoredInvoiceReview))!;
        var audit = db.Model.FindEntityType(typeof(StoredInvoiceReviewEvent))!;
        Assert.NotNull(review);
        Assert.NotNull(audit);
        Assert.Equal("invoice_reviews", review.GetTableName());
        Assert.Equal("invoice_review_events", audit.GetTableName());
        Assert.True(review.FindProperty(nameof(StoredInvoiceReview.Revision))!.IsConcurrencyToken);
        Assert.Equal(3, review.GetForeignKeys().Single(x =>
            x.PrincipalEntityType.ClrType == typeof(StoredInvoice)).Properties.Count);
        Assert.Equal(3, audit.GetForeignKeys().Single(x =>
            x.PrincipalEntityType.ClrType == typeof(StoredInvoiceReview)).Properties.Count);
        Assert.Contains(audit.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(y => y.Name).SequenceEqual(new[] {
                nameof(StoredInvoiceReviewEvent.InvoiceId), nameof(StoredInvoiceReviewEvent.Revision) }));
    }
}
