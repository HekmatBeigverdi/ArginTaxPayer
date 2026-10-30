using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10Step28SalesReturnValidationTests
{
    private static InvoiceEntryLine Line(decimal quantity=5m, decimal price=100m) =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "کالا",quantity,price,0m,10m,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "LOCAL","1234567890123");

    [Fact]
    public void PartialReturnWithinRemainingQuantityIsValid() =>
        Assert.True(Phase10SalesReturnValidation.AllowedLines(
            [Line()], [Line(2m)], [1m]));

    [Fact]
    public void ExcessAcrossMultipleReturnsIsRejected() =>
        Assert.False(Phase10SalesReturnValidation.AllowedLines(
            [Line()], [Line(3m)], [3m]));

    [Fact]
    public void ZeroReturnIsRejected() =>
        Assert.False(Phase10SalesReturnValidation.AllowedLines(
            [Line()], [Line(0m)], [0m]));

    [Fact]
    public void ChangedPriceIsRejected() =>
        Assert.False(Phase10SalesReturnValidation.AllowedLines(
            [Line()], [Line(1m,200m)], [0m]));

    [Fact]
    public void IncorrectNumberOfLinesIsRejected() =>
        Assert.False(Phase10SalesReturnValidation.AllowedLines(
            [Line()], [], [0m]));
}
