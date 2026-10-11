using ArginTaxPayer.Application;
using ArginTaxPayer.Application.Abstractions.Idempotency;
using ArginTaxPayer.Application.Abstractions.Tenancy;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdempotencyFoundationTests
{
    private const string Fingerprint =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static ArginTaxPayerDbContext Context() =>
        new(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=x;Password=x").Options);

    [Fact]
    public void ReservationHasTenantOperationKeyUniqueIndexAndMigration()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(IdempotencyReservation));
        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(IdempotencyReservation.TaxpayerId),
                nameof(IdempotencyReservation.Operation),
                nameof(IdempotencyReservation.Key)
            }));
        Assert.Contains("20261011180000_AddIdempotencyReservations", db.Database.GetMigrations());
    }

    [Fact]
    public async Task ReservationStagesInDbContextWithoutDatabaseAccess()
    {
        using var db = Context();
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var taxpayer = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<ITaxpayerScopeInitializer>().Initialize(taxpayer);
        IIdempotencyReservationWriter writer = new EfIdempotencyReservationWriter(
            db, scope.ServiceProvider.GetRequiredService<ITaxpayerScope>());

        await writer.ReserveAsync(Guid.NewGuid(), "invoice.submit", "request-1", Fingerprint);

        var record = Assert.Single(db.ChangeTracker.Entries<IdempotencyReservation>());
        Assert.Equal(EntityState.Added, record.State);
        Assert.Equal(taxpayer, record.Entity.TaxpayerId);
        Assert.Equal(Fingerprint, record.Entity.RequestFingerprint);
    }

    [Fact]
    public async Task UninitializedTenantFailsClosed()
    {
        using var db = Context();
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var writer = new EfIdempotencyReservationWriter(
            db, scope.ServiceProvider.GetRequiredService<ITaxpayerScope>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            writer.ReserveAsync(Guid.NewGuid(), "invoice.submit", "request-1", Fingerprint));
        Assert.Empty(db.ChangeTracker.Entries<IdempotencyReservation>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("xyz")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg")]
    public async Task InvalidFingerprintIsRejected(string fingerprint)
    {
        using var db = Context();
        var services = new ServiceCollection();
        services.AddArginTaxPayerApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITaxpayerScopeInitializer>().Initialize(Guid.NewGuid());
        var writer = new EfIdempotencyReservationWriter(
            db, scope.ServiceProvider.GetRequiredService<ITaxpayerScope>());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            writer.ReserveAsync(Guid.NewGuid(), "invoice.submit", "key", fingerprint));
    }
}
