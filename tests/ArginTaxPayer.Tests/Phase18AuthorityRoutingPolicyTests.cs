using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18AuthorityRoutingPolicyTests
{
    private static AuthorityOperationBinding Base(TaxAuthorityEnvironment environment) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), environment);

    [Theory]
    [InlineData(TaxAuthorityEnvironment.Sandbox)]
    [InlineData(TaxAuthorityEnvironment.Production)]
    public void Exact_persisted_memory_and_provider_environment_required(TaxAuthorityEnvironment kind)
    {
        var op = Base(kind);
        Assert.True(Phase18AuthorityRoutingPolicy.CanUseProvider(op, op, kind));
        var wrong = kind == TaxAuthorityEnvironment.Sandbox ?
            TaxAuthorityEnvironment.Production : TaxAuthorityEnvironment.Sandbox;
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseProvider(op, op, wrong));
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseMemory(op,
            op with { Environment = wrong }));
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseProvider(op, op, TaxAuthorityEnvironment.Unknown));
    }

    [Fact]
    public void Reject_unknown_cross_tenant_wrong_taxpayer_or_memory()
    {
        var op = Base(TaxAuthorityEnvironment.Sandbox);
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseMemory(
            op with { Environment = TaxAuthorityEnvironment.Unknown }, op));
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseMemory(op,
            op with { CustomerUserId = Guid.NewGuid() }));
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseMemory(op,
            op with { TaxpayerId = Guid.NewGuid() }));
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseMemory(op,
            op with { FiscalMemoryId = Guid.NewGuid() }));
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseMemory(
            op with { InvoiceId = Guid.Empty }, op));
    }

    [Fact]
    public void Legal_successor_inherits_environment_scope_without_reusing_invoice_identity()
    {
        var original = Base(TaxAuthorityEnvironment.Production);
        var child = original with { InvoiceId = Guid.NewGuid() };
        Assert.True(Phase18AuthorityRoutingPolicy.CanInheritLegalSuccessor(original, child));
        Assert.False(Phase18AuthorityRoutingPolicy.CanInheritLegalSuccessor(original, original));
        Assert.False(Phase18AuthorityRoutingPolicy.CanInheritLegalSuccessor(original,
            child with { Environment = TaxAuthorityEnvironment.Sandbox }));
        Assert.False(Phase18AuthorityRoutingPolicy.CanInheritLegalSuccessor(original,
            child with { FiscalMemoryId = Guid.NewGuid() }));
    }
}
