using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ManualCatalogMappingPostgresTests
{
    [Fact]
    public async Task Manual_codes_follow_release_guards_and_can_be_mapped_and_reused()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(connection).Database);
        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(connection).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        var first = await TaxIdentifierMappingService.GetOrCreateManualEntryAsync(db, "2710000113478", 1);
        var second = await TaxIdentifierMappingService.GetOrCreateManualEntryAsync(db, "2720000249213", 1);
        var reused = await TaxIdentifierMappingService.GetOrCreateManualEntryAsync(db, "2710000113478", 1);
        Assert.Equal(first.Id, reused.Id);
        Assert.NotEqual(first.ReleaseId, second.ReleaseId);
        Assert.Equal(3, (await db.CatalogReferenceReleases.SingleAsync(x => x.Id == first.ReleaseId)).State);

        // Use an existing owner's unit only as a fixture; all writes roll back.
        var existing = await db.CustomerCatalogItems.AsNoTracking().FirstAsync();
        var item = new CustomerCatalogItem {
            Id = Guid.NewGuid(), CustomerUserId = existing.CustomerUserId,
            BaseUnitId = existing.BaseUnitId, InternalCode = Guid.NewGuid().ToString("N"),
            Title = "تست نگاشت", Kind = 1, Status = 1, Revision = 1,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.CustomerCatalogItems.Add(item);
        await db.SaveChangesAsync();
        var mapping = new CatalogOfficialMapping {
            Id = Guid.NewGuid(), ItemId = item.Id, OfficialEntryId = first.Id,
            EffectiveFromUtc = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc), Revision = 1
        };
        db.CatalogOfficialMappings.Add(mapping);
        await db.SaveChangesAsync();
        Assert.True(await db.CatalogOfficialMappings.AsNoTracking().AnyAsync(x => x.Id == mapping.Id));
        var codes = await CatalogSearchService.CurrentCodesAsync(db, item.CustomerUserId,
            [item.Id], mapping.EffectiveFromUtc);
        Assert.Equal("2710000113478", codes[item.Id]);
        Assert.Empty(await CatalogSearchService.CurrentCodesAsync(db, Guid.NewGuid(),
            [item.Id], mapping.EffectiveFromUtc));
        Assert.Empty(await CatalogSearchService.CurrentCodesAsync(db, item.CustomerUserId,
            [item.Id], mapping.EffectiveFromUtc.AddDays(-1)));
        mapping.EffectiveToUtc = mapping.EffectiveFromUtc.AddDays(1);
        mapping.Revision++;
        await db.SaveChangesAsync();
        Assert.Empty(await CatalogSearchService.CurrentCodesAsync(db, item.CustomerUserId,
            [item.Id], mapping.EffectiveToUtc.Value));
        await tx.RollbackAsync();
    }
}
