using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FiscalMemoryPersistenceTests
{
    [Fact]
    public void Fiscal_memory_requires_a_taxpayer_and_database_enforces_active_uniqueness()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused")
            .Options;
        using var db = new ArginTaxPayerDbContext(options);
        var entity = db.Model.FindEntityType(typeof(FiscalMemory));
        Assert.NotNull(entity);
        Assert.Equal("fiscal_memories", entity!.GetTableName());
        Assert.Single(entity.GetForeignKeys());
        var fk = entity.GetForeignKeys().Single();
        Assert.Equal(typeof(TaxpayerProfile), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.False(fk.IsRequired == false);
        Assert.Contains(entity.GetIndexes(), i =>
            i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "MemoryIdentifier" }));
        Assert.Contains(entity.GetIndexes(), i =>
            i.IsUnique && i.GetFilter() == "status = 2" &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TaxpayerId" }));
        Assert.DoesNotContain(entity.GetProperties(), p =>
            p.Name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(FiscalMemoryStatus.PendingVerification, FiscalMemoryStatus.Active, true)]
    [InlineData(FiscalMemoryStatus.Active, FiscalMemoryStatus.Suspended, true)]
    [InlineData(FiscalMemoryStatus.Suspended, FiscalMemoryStatus.Active, true)]
    [InlineData(FiscalMemoryStatus.Active, FiscalMemoryStatus.Retired, true)]
    [InlineData(FiscalMemoryStatus.Retired, FiscalMemoryStatus.Active, false)]
    public void Fiscal_memory_transitions_remain_declared(FiscalMemoryStatus from, FiscalMemoryStatus to, bool expected)
        => Assert.Equal(expected, TaxpayerLifecyclePolicy.CanTransition(from, to));
}
