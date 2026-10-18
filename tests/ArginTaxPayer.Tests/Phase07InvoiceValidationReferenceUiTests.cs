using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoiceValidationReferenceUiTests
{
    [Fact]
    public void Validation_view_never_authorizes_external_submission()
    {
        var validation = new InvoiceUiValidation(Guid.NewGuid(),3,"RC_IITP.IS_V7.9",
            false,[new InvoiceUiFinding("invoice.rules.not_approved","warning",
                "validation","Official validation is pending")]);
        Assert.False(validation.IsSubmissionAuthorized);
        Assert.Equal("warning",Assert.Single(validation.Findings).Severity);
    }

    [Fact]
    public void Reference_view_separates_internal_invoice_and_verified_fiscal_id()
    {
        var row = new InvoiceUiReference(Guid.NewGuid(),"Original","Draft",
            DateTime.UtcNow,1,false,null);
        Assert.False(row.FiscalIdentifierVerified);
        Assert.Null(row.FiscalIdentifier);
        Assert.NotEqual(Guid.Empty,row.Id);
    }

    [Fact]
    public void Query_surface_exposes_read_only_scoped_reference_and_validation()
    {
        Assert.NotNull(typeof(InvoiceValidationReferenceService)
            .GetMethod("ValidateAsync"));
        Assert.NotNull(typeof(InvoiceValidationReferenceService)
            .GetMethod("ListReferencesAsync"));
        Assert.Null(typeof(InvoiceValidationReferenceService)
            .GetMethod("SubmitAsync"));
        Assert.Null(typeof(InvoiceValidationReferenceService)
            .GetMethod("ApproveReferenceAsync"));
    }
}
