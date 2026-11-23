using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18InvoiceParticipantIdentityPolicyTests
{
    [Fact]
    public void Missing_seller_identifier_reports_seller_even_when_buyer_is_also_missing()
    {
        Assert.Equal("seller_economic_identifier_missing",
            InvoiceParticipantIdentityPolicy.MissingCode(null, null, buyerRequired: true));
    }

    [Fact]
    public void Missing_required_buyer_identifier_reports_buyer()
    {
        Assert.Equal("buyer_identity_missing",
            InvoiceParticipantIdentityPolicy.MissingCode("12345678901", null, buyerRequired: true));
    }

    [Fact]
    public void Type_two_invoice_does_not_require_buyer_identity()
    {
        Assert.Null(InvoiceParticipantIdentityPolicy.MissingCode(
            "12345678901", null, buyerRequired: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Whitespace_seller_identifier_is_missing(string sellerIdentifier)
    {
        Assert.Equal("seller_economic_identifier_missing",
            InvoiceParticipantIdentityPolicy.MissingCode(sellerIdentifier, "98765432101", buyerRequired: true));
    }
}
