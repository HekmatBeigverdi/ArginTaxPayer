using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ManualCatalogMappingPostgresTests
{
    [Fact]
    public async Task Catalog_items_store_official_codes_and_allow_repeated_internal_codes()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(connection).Database);
        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(connection).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        // Use an existing owner's unit only as a fixture; all writes roll back.
        var existing = await db.CustomerCatalogItems.AsNoTracking().FirstAsync();
        var item = new CustomerCatalogItem {
            Id = Guid.NewGuid(), CustomerUserId = existing.CustomerUserId,
            BaseUnitId = existing.BaseUnitId, InternalCode = "REBAR",
            OfficialCode = "2710000113478", Title = "میلگرد ۵ میل", Kind = 1, Status = 1, Revision = 1,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        var secondItem = new CustomerCatalogItem {
            Id = Guid.NewGuid(), CustomerUserId = existing.CustomerUserId,
            BaseUnitId = existing.BaseUnitId, InternalCode = "REBAR",
            OfficialCode = "2720000249213", Title = "میلگرد ۸ میل", Kind = 1, Status = 1, Revision = 1,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.CustomerCatalogItems.AddRange(item, secondItem);
        await db.SaveChangesAsync();
        var items = await db.CustomerCatalogItems.AsNoTracking()
            .Where(x => x.CustomerUserId == existing.CustomerUserId && x.InternalCode == "REBAR")
            .ToDictionaryAsync(x => x.Id);
        Assert.Equal("2710000113478", items[item.Id].OfficialCode);
        Assert.Equal("2720000249213", items[secondItem.Id].OfficialCode);
        await tx.RollbackAsync();
    }
}
