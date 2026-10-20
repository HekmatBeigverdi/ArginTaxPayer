using ArginTaxPayer.Api.Invoices;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08InvoiceReviewApiContractTests
{
    [Theory]
    [InlineData(InvoiceReviewCommandDecision.Unauthenticated, 401)]
    [InlineData(InvoiceReviewCommandDecision.Forbidden, 403)]
    [InlineData(InvoiceReviewCommandDecision.NotFound, 404)]
    [InlineData(InvoiceReviewCommandDecision.Invalid, 400)]
    [InlineData(InvoiceReviewCommandDecision.Conflict, 409)]
    [InlineData(InvoiceReviewCommandDecision.IdempotencyKeyConflict, 409)]
    [InlineData(InvoiceReviewCommandDecision.Success, 200)]
    public void Decision_has_stable_http_status(InvoiceReviewCommandDecision decision, int status)
    {
        var response = InvoiceReviewDevelopmentEndpoints.MapResult(
            new InvoiceReviewCommandResult(decision, InvoiceReviewState.Approved, 3));
        Assert.Equal(status, ((IStatusCodeHttpResult)response).StatusCode);
    }

    [Fact]
    public void Public_api_does_not_offer_authority_submission_operation()
    {
        Assert.DoesNotContain(Enum.GetNames<InvoiceReviewAction>(),
            x => x.Contains("SubmitToAuthority", StringComparison.OrdinalIgnoreCase));
    }
}
