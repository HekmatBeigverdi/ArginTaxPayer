using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08InvoiceReviewIdempotencyTests
{
    [Fact]
    public void Durable_request_entity_has_scoped_unique_key_and_immutable_outcome_shape()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=none;Password=none")
            .Options;
        using var db = new ArginTaxPayerDbContext(options);
        var entity = db.Model.FindEntityType(typeof(StoredInvoiceReviewRequest))!;
        Assert.Equal("invoice_review_requests", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] {
                nameof(StoredInvoiceReviewRequest.CustomerUserId),
                nameof(StoredInvoiceReviewRequest.TaxpayerId),
                nameof(StoredInvoiceReviewRequest.ActorUserId),
                nameof(StoredInvoiceReviewRequest.IdempotencyKey)
            }));
        Assert.Equal(3, entity.GetForeignKeys().Single(fk =>
            fk.PrincipalEntityType.ClrType == typeof(StoredInvoiceReview)).Properties.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\nkey")]
    public async Task Invalid_idempotency_key_is_rejected_without_database_access(string? key)
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=none;Password=none")
            .Options;
        await using var db = new ArginTaxPayerDbContext(options);
        var service = new InvoiceReviewCommandService(db, null!);
        var result = await service.ExecuteAsync(null, new InvoiceReviewCommand(
            Guid.NewGuid(), 1, InvoiceReviewAction.SubmitForReview,
            IdempotencyKey: key));
        Assert.Equal(InvoiceReviewCommandDecision.Invalid, result.Decision);
    }

    [Fact]
    public async Task Overlength_key_is_rejected_without_database_access()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=none;Password=none")
            .Options;
        await using var db = new ArginTaxPayerDbContext(options);
        var service = new InvoiceReviewCommandService(db, null!);
        var result = await service.ExecuteAsync(null, new InvoiceReviewCommand(
            Guid.NewGuid(), 1, InvoiceReviewAction.SubmitForReview,
            IdempotencyKey: new string('a', 129)));
        Assert.Equal(InvoiceReviewCommandDecision.Invalid, result.Decision);
    }
}
