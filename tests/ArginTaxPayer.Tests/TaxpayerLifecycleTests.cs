using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class TaxpayerLifecycleTests
{
    [Theory]
    [InlineData(TaxpayerStatus.Active, TaxpayerStatus.Suspended, true)]
    [InlineData(TaxpayerStatus.Suspended, TaxpayerStatus.Active, true)]
    [InlineData(TaxpayerStatus.Active, TaxpayerStatus.Archived, true)]
    [InlineData(TaxpayerStatus.Suspended, TaxpayerStatus.Archived, true)]
    [InlineData(TaxpayerStatus.Archived, TaxpayerStatus.Active, false)]
    [InlineData(TaxpayerStatus.Archived, TaxpayerStatus.Suspended, false)]
    public void State_machine_enforces_terminal_archive(TaxpayerStatus from, TaxpayerStatus to, bool allowed)
        => Assert.Equal(allowed, TaxpayerLifecyclePolicy.CanTransition(from, to));

    [Fact]
    public void Profile_history_is_mapped_with_foreign_keys_and_unique_revision()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options;
        using var db = new ArginTaxPayerDbContext(options);
        var entity = db.Model.FindEntityType(typeof(TaxpayerProfileChange))!;
        Assert.NotNull(entity);
        Assert.Equal("taxpayer_profile_changes", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), i =>
            i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TaxpayerId", "ToRevision" }));
        Assert.Equal(2, entity.GetForeignKeys().Count());
    }
}
