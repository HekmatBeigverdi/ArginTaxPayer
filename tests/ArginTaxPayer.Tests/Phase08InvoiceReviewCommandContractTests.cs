using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08InvoiceReviewCommandContractTests
{
    [Fact]
    public async Task Invalid_operations_fail_before_any_database_access()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=none;Password=none")
            .Options;
        await using var db = new ArginTaxPayerDbContext(options);
        // Invalid-path validation must not dereference the unavailable scope service.
        var service = new InvoiceReviewCommandService(db, null!);
        var invalid = new InvoiceReviewCommand(Guid.Empty, 1,
            InvoiceReviewAction.SubmitForReview);
        var result = await service.ExecuteAsync(null, invalid);
        Assert.Equal(InvoiceReviewCommandDecision.Invalid, result.Decision);
        Assert.Null(result.State);
    }

    [Theory]
    [InlineData(InvoiceReviewAction.RequestChanges, null)]
    [InlineData(InvoiceReviewAction.RequestChanges, "")]
    [InlineData(InvoiceReviewAction.SubmitForReview, "unexpected")]
    [InlineData(InvoiceReviewAction.Approve, "unexpected")]
    public async Task Invalid_reason_is_rejected_before_access_check(
        InvoiceReviewAction action, string? reason)
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=argintaxpayer_test;Username=none;Password=none")
            .Options;
        await using var db = new ArginTaxPayerDbContext(options);
        var service = new InvoiceReviewCommandService(db, null!);
        var result = await service.ExecuteAsync(null,
            new InvoiceReviewCommand(Guid.NewGuid(), 1, action, reason));
        Assert.Equal(InvoiceReviewCommandDecision.Invalid, result.Decision);
    }
}
