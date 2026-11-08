using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18PersistedAuthorityEnvironmentResolverTests
{
    [Fact]
    public void Null_and_unrecognized_memory_environment_fail_closed()
    {
        Assert.Equal(TaxAuthorityEnvironment.Unknown,
            Phase18PersistedAuthorityEnvironmentResolver.ToAuthorityEnvironment(null));
        Assert.Equal(TaxAuthorityEnvironment.Unknown,
            Phase18PersistedAuthorityEnvironmentResolver.ToAuthorityEnvironment(
                (FiscalConnectionEnvironment)999));
    }

    [Fact]
    public void Production_is_mapped_by_name_not_by_underlying_numeric_value()
    {
        Assert.Equal(1, (int)FiscalConnectionEnvironment.Production);
        Assert.Equal(2, (int)TaxAuthorityEnvironment.Production);
        Assert.Equal(TaxAuthorityEnvironment.Production,
            Phase18PersistedAuthorityEnvironmentResolver.ToAuthorityEnvironment(
                FiscalConnectionEnvironment.Production));
    }

    [Fact]
    public void Sandbox_is_mapped_by_name_not_by_underlying_numeric_value()
    {
        Assert.Equal(2, (int)FiscalConnectionEnvironment.Sandbox);
        Assert.Equal(1, (int)TaxAuthorityEnvironment.Sandbox);
        Assert.Equal(TaxAuthorityEnvironment.Sandbox,
            Phase18PersistedAuthorityEnvironmentResolver.ToAuthorityEnvironment(
                FiscalConnectionEnvironment.Sandbox));
    }
}
