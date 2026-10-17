using ArginTaxPayer.Api.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Offline API contract tests. Authenticated HTTP/PostgreSQL smoke is a
/// separate operator exercise; no development endpoints perform tax sending.
/// </summary>
public sealed class Phase07InvoiceApiContractTests
{
    [Fact]
    public void Api_exposes_scoped_development_invoice_draft_route_mapper()
    {
        Assert.Equal("InvoiceDraftDevelopmentEndpoints",
            typeof(InvoiceDraftDevelopmentEndpoints).Name);
        Assert.NotNull(typeof(InvoiceDraftDevelopmentEndpoints)
            .GetMethod("MapInvoiceDraftDevelopmentEndpoints"));
    }

    [Fact]
    public void Draft_write_request_contains_no_client_selected_owner_or_tax_state()
    {
        var properties = typeof(CreateInvoiceDraftRequest).GetProperties()
            .Select(x => x.Name).ToArray();
        Assert.Contains("IssueDateUtc", properties);
        Assert.Contains("SellerDisplayName", properties);
        Assert.DoesNotContain("CustomerUserId", properties);
        Assert.DoesNotContain("TaxpayerId", properties);
        Assert.DoesNotContain("State", properties);
        Assert.DoesNotContain("FiscalTaxId", properties);
        Assert.DoesNotContain("Subject", properties);
    }

    [Fact]
    public void Api_decisions_do_not_include_tax_authority_send_success()
    {
        var names = Enum.GetNames<InvoiceApiDecision>();
        Assert.Contains(nameof(InvoiceApiDecision.Unauthenticated), names);
        Assert.Contains(nameof(InvoiceApiDecision.Forbidden), names);
        Assert.Contains(nameof(InvoiceApiDecision.NotFound), names);
        Assert.Contains(nameof(InvoiceApiDecision.Invalid), names);
        Assert.Contains(nameof(InvoiceApiDecision.Conflict), names);
        Assert.DoesNotContain("Submitted", names);
        Assert.DoesNotContain("TaxAuthorityAccepted", names);
    }

    [Fact]
    public void Response_contains_revision_and_internal_status()
    {
        var response = new InvoiceDraftView(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, "Draft", "Original", DateTime.UtcNow, DateTime.UtcNow);
        Assert.Equal(1, response.Revision);
        Assert.Equal("Draft", response.State);
        Assert.Equal("Original", response.Subject);
    }

    [Fact]
    public void Service_has_typed_authenticated_list_get_and_create_contracts()
    {
        var type = typeof(InvoiceDraftApplicationService);
        Assert.NotNull(type.GetMethod("CreateAsync"));
        Assert.NotNull(type.GetMethod("GetAsync"));
        Assert.NotNull(type.GetMethod("ListAsync"));
        Assert.Null(type.GetMethod("SendAsync"));
        Assert.Null(type.GetMethod("FinalizeAsync"));
    }
}
