using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18PersistedAuthorityOperationGateTests
{
    [Theory]
    [InlineData(0, TaxAuthorityEnvironment.Unknown)]
    [InlineData(1, TaxAuthorityEnvironment.Production)]
    [InlineData(2, TaxAuthorityEnvironment.Sandbox)]
    [InlineData(-1, TaxAuthorityEnvironment.Unknown)]
    [InlineData(3, TaxAuthorityEnvironment.Unknown)]
    public void Persisted_environment_uses_fiscal_memory_mapping_not_routing_enum(
        int persisted, TaxAuthorityEnvironment expected)
    {
        Assert.Equal(expected,
            Phase18PersistedAuthorityOperationGate.FromPersistedValue(persisted));
    }

    [Fact]
    public void Invalid_provider_enum_does_not_authorize_persisted_operation()
    {
        var binding = new AuthorityOperationBinding(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            TaxAuthorityEnvironment.Production);
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseProvider(
            binding, binding, (TaxAuthorityEnvironment)123));
        Assert.False(Phase18AuthorityRoutingPolicy.CanUseMemory(
            binding with { Environment = (TaxAuthorityEnvironment)123 }, binding));
    }

    [Fact]
    public void Cross_environment_legal_successor_is_forbidden()
    {
        var original = new AuthorityOperationBinding(Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TaxAuthorityEnvironment.Sandbox);
        var child = original with { InvoiceId = Guid.NewGuid(),
            Environment = TaxAuthorityEnvironment.Production };
        Assert.False(Phase18AuthorityRoutingPolicy.CanInheritLegalSuccessor(original, child));
    }
}
