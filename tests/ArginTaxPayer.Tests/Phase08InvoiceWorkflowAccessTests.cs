using ArginTaxPayer.Application.Tenancy;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08InvoiceWorkflowAccessTests
{
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Taxpayer = Guid.NewGuid();
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid FormerPartner = Guid.NewGuid();
    private static InvoiceWorkflowScope Scope(Guid actor, Guid? partner = null, bool active = true) =>
        new(actor, Customer, Taxpayer, Root, partner ?? Partner, active);
    private static bool Allowed(InvoiceWorkflowRole role, InvoiceWorkflowPermission permission,
        InvoiceWorkflowScope? scope) =>
        InvoiceWorkflowAccessPolicy.Evaluate(role, permission, scope) == InvoiceWorkflowAccessDecision.Allowed;

    [Theory]
    [InlineData(InvoiceWorkflowPermission.Read)]
    [InlineData(InvoiceWorkflowPermission.EditDraft)]
    [InlineData(InvoiceWorkflowPermission.SubmitForReview)]
    public void Customer_can_read_edit_and_submit_own_taxpayer(InvoiceWorkflowPermission permission) =>
        Assert.True(Allowed(InvoiceWorkflowRole.Customer, permission, Scope(Customer)));

    [Theory]
    [InlineData(InvoiceWorkflowPermission.RequestChanges)]
    [InlineData(InvoiceWorkflowPermission.Approve)]
    public void Customer_cannot_review_or_approve(InvoiceWorkflowPermission permission) =>
        Assert.False(Allowed(InvoiceWorkflowRole.Customer, permission, Scope(Customer)));

    [Theory]
    [InlineData(InvoiceWorkflowRole.Level1)]
    [InlineData(InvoiceWorkflowRole.Level2)]
    public void Current_administrators_can_read_and_review_but_not_edit_as_customer(
        InvoiceWorkflowRole role)
    {
        var scope = Scope(role == InvoiceWorkflowRole.Level1 ? Root : Partner);
        Assert.True(Allowed(role, InvoiceWorkflowPermission.Read, scope));
        Assert.True(Allowed(role, InvoiceWorkflowPermission.Approve, scope));
        Assert.True(Allowed(role, InvoiceWorkflowPermission.RequestChanges, scope));
        Assert.False(Allowed(role, InvoiceWorkflowPermission.EditDraft, scope));
        Assert.False(Allowed(role, InvoiceWorkflowPermission.SubmitForReview, scope));
    }

    [Fact]
    public void Former_partner_and_other_customer_are_denied_even_for_read()
    {
        foreach (var p in Enum.GetValues<InvoiceWorkflowPermission>())
        {
            Assert.False(Allowed(InvoiceWorkflowRole.Level2, p, Scope(FormerPartner)));
            Assert.False(Allowed(InvoiceWorkflowRole.Customer, p, Scope(Guid.NewGuid())));
            Assert.False(Allowed(InvoiceWorkflowRole.Level1, p, Scope(Guid.NewGuid())));
        }
    }

    [Fact]
    public void Unlinked_taxpayer_and_missing_or_invalid_scope_fail_closed()
    {
        foreach (var role in Enum.GetValues<InvoiceWorkflowRole>())
            foreach (var permission in Enum.GetValues<InvoiceWorkflowPermission>())
            {
                var actor = role switch {
                    InvoiceWorkflowRole.Customer => Customer,
                    InvoiceWorkflowRole.Level1 => Root,
                    _ => Partner
                };
                Assert.False(Allowed(role, permission, Scope(actor, active: false)));
                Assert.False(Allowed(role, permission, null));
            }
        Assert.False(Allowed((InvoiceWorkflowRole)999, InvoiceWorkflowPermission.Read, Scope(Customer)));
        Assert.False(Allowed(InvoiceWorkflowRole.Customer, (InvoiceWorkflowPermission)999, Scope(Customer)));
        Assert.Throws<ArgumentException>(() => new InvoiceWorkflowScope(
            Customer, Customer, Guid.Empty, Root, Partner, true));
    }

    [Fact]
    public void Root_direct_customer_requires_no_partner_for_root_access()
    {
        var scope = new InvoiceWorkflowScope(Root, Customer, Taxpayer, Root, null, true);
        Assert.True(Allowed(InvoiceWorkflowRole.Level1, InvoiceWorkflowPermission.Approve, scope));
        Assert.False(Allowed(InvoiceWorkflowRole.Level2, InvoiceWorkflowPermission.Read, scope));
    }
}
