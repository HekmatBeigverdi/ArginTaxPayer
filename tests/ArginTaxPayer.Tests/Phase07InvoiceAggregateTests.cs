using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoiceAggregateTests
{
    private static readonly DateTime Utc = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly InvoicePartySnapshot Seller =
        new(Guid.NewGuid(), "شرکت فروشنده", "12345678901", "1234567890");

    private static InvoiceAggregate NewDraft() => InvoiceAggregate.CreateDraft(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Utc, Utc, Seller);

    [Fact]
    public void New_invoice_has_immutable_scope_and_draft_revision()
    {
        var invoice = NewDraft();
        Assert.NotEqual(Guid.Empty, invoice.Id);
        Assert.NotEqual(Guid.Empty, invoice.CustomerUserId);
        Assert.NotEqual(Guid.Empty, invoice.TaxpayerId);
        Assert.Equal(InvoiceLifecycleState.Draft, invoice.State);
        Assert.Equal(1, invoice.Revision);
        Assert.Equal(Utc, invoice.IssueDateUtc);
        Assert.Null(invoice.Buyer);
    }

    [Fact]
    public void Scope_and_identity_cannot_be_changed_by_public_setters()
    {
        var immutable = new[] { nameof(InvoiceAggregate.Id),
            nameof(InvoiceAggregate.CustomerUserId), nameof(InvoiceAggregate.TaxpayerId),
            nameof(InvoiceAggregate.FiscalMemoryId), nameof(InvoiceAggregate.CreatedAtUtc) };
        foreach (var name in immutable)
            Assert.Null(typeof(InvoiceAggregate).GetProperty(name)!.SetMethod);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Empty_identity_is_rejected(int position)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[position] = Guid.Empty;
        Assert.Throws<ArgumentException>(() => InvoiceAggregate.CreateDraft(
            ids[0], ids[1], ids[2], null, Utc, Utc, Seller));
    }

    [Fact]
    public void Invalid_optional_memory_or_local_date_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => InvoiceAggregate.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Utc, Utc, Seller));
        Assert.Throws<ArgumentException>(() => InvoiceAggregate.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            DateTime.SpecifyKind(Utc, DateTimeKind.Local), Utc, Seller));
        Assert.Throws<ArgumentException>(() => InvoiceAggregate.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Utc, DateTime.SpecifyKind(Utc, DateTimeKind.Unspecified), Seller));
    }

    [Fact]
    public void Participant_is_normalized_and_invalid_names_rejected()
    {
        var snapshot = new InvoicePartySnapshot(null, "  فروشنده  ", "123 ", "12345 ");
        Assert.Equal("فروشنده", snapshot.DisplayName);
        Assert.Equal("123", snapshot.NationalOrEconomicId);
        Assert.Throws<ArgumentException>(() => new InvoicePartySnapshot(null, "  "));
        Assert.Throws<ArgumentException>(() => new InvoicePartySnapshot(Guid.Empty, "فروشنده"));
    }

    [Fact]
    public void Updating_details_bumps_revision_only_on_changes()
    {
        var invoice = NewDraft();
        invoice.UpdateDraftDetails(Utc, Seller, null);
        Assert.Equal(1, invoice.Revision);
        var buyer = new InvoicePartySnapshot(Guid.NewGuid(), "خریدار");
        invoice.UpdateDraftDetails(Utc.AddDays(1), Seller, buyer);
        Assert.Equal(2, invoice.Revision);
        Assert.Same(buyer, invoice.Buyer);
        invoice.UpdateDraftDetails(Utc.AddDays(1), Seller, buyer);
        Assert.Equal(2, invoice.Revision);
        Assert.Throws<ArgumentException>(() => invoice.UpdateDraftDetails(
            DateTime.SpecifyKind(Utc, DateTimeKind.Local), Seller, null));
        Assert.Equal(2, invoice.Revision);
    }

    [Fact]
    public void No_public_issuance_or_tax_calculation_is_exposed()
    {
        var names = typeof(InvoiceAggregate).GetMethods()
            .Where(m => m.DeclaringType == typeof(InvoiceAggregate))
            .Select(m => m.Name).ToArray();
        Assert.DoesNotContain("Issue", names);
        Assert.DoesNotContain("Submit", names);
        Assert.DoesNotContain("CalculateVat", names);
    }
}
