using ArginTaxPayer.Application;
using ArginTaxPayer.Application.Abstractions.Messaging;
using ArginTaxPayer.Application.Abstractions.Tenancy;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class OutboxFoundationTests
{
    private static ArginTaxPayerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void OutboxIsMappedWithUniquePerTaxpayerDeduplicationKey()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(OutboxMessage));
        Assert.NotNull(entity);
        Assert.Equal("outbox_messages", entity!.GetTableName());
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(OutboxMessage.TaxpayerId), nameof(OutboxMessage.DeduplicationKey) }));
        Assert.Contains("20261011120000_AddOutboxMessages", db.Database.GetMigrations());
    }

    [Fact]
    public async Task EnqueueAddsScopedMessageWithoutSavingOrConnecting()
    {
        using var db = CreateContext();
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var taxpayer = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<ITaxpayerScopeInitializer>().Initialize(taxpayer);
        var writer = new EfOutboxWriter(db, scope.ServiceProvider.GetRequiredService<ITaxpayerScope>());
        var id = Guid.NewGuid();

        await writer.EnqueueAsync(id, "invoice.created", "{}", "source:invoice:1");

        var tracked = Assert.Single(db.ChangeTracker.Entries<OutboxMessage>());
        Assert.Equal(EntityState.Added, tracked.State);
        Assert.Equal(taxpayer, tracked.Entity.TaxpayerId);
        Assert.Equal(id, tracked.Entity.Id);
        Assert.Equal("source:invoice:1", tracked.Entity.DeduplicationKey);
    }

    [Fact]
    public async Task EnqueueFailsClosedWithoutInitializedTaxpayerScope()
    {
        using var db = CreateContext();
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var writer = new EfOutboxWriter(db, scope.ServiceProvider.GetRequiredService<ITaxpayerScope>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            writer.EnqueueAsync(Guid.NewGuid(), "invoice.created", "{}", "key-1"));
        Assert.Empty(db.ChangeTracker.Entries<OutboxMessage>());
    }

    [Fact]
    public void ApplicationContractDoesNotDependOnInfrastructure()
    {
        Assert.Equal("ArginTaxPayer.Application",
            typeof(IOutboxWriter).Assembly.GetName().Name);
    }
}
