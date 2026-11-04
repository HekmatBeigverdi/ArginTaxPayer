using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18FiscalMemoryEnvironmentPersistenceTests
{
    [Fact]
    public void Legacy_fiscal_memory_stays_unclassified()
    {
        var memory = new FiscalMemory { Id = Guid.NewGuid(), TaxpayerId = Guid.NewGuid(), MemoryIdentifier = "ABC123" };
        Assert.Null(memory.AuthorityEnvironment);
    }

    [Theory]
    [InlineData(FiscalConnectionEnvironment.Sandbox)]
    [InlineData(FiscalConnectionEnvironment.Production)]
    public void Explicit_classification_is_visible_without_global_environment_inference(FiscalConnectionEnvironment environment)
    {
        var request = new RegisterFiscalMemoryRequest("ABC123", environment);
        Assert.Equal(environment, request.AuthorityEnvironment);
        var memory = new FiscalMemory { AuthorityEnvironment = request.AuthorityEnvironment };
        Assert.Equal(environment, memory.AuthorityEnvironment);
        var view = new FiscalMemoryView(Guid.NewGuid(), Guid.NewGuid(), "ABC123", FiscalMemoryStatus.PendingVerification, 1, memory.AuthorityEnvironment);
        Assert.Equal(environment, view.AuthorityEnvironment);
    }

    [Fact]
    public void Unclassified_registration_request_remains_distinct_from_explicit_production()
    {
        Assert.Null(new RegisterFiscalMemoryRequest("ABC123").AuthorityEnvironment);
        Assert.NotEqual((FiscalConnectionEnvironment?)FiscalConnectionEnvironment.Production,
            new RegisterFiscalMemoryRequest("ABC123").AuthorityEnvironment);
    }
}
