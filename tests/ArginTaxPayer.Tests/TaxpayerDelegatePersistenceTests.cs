using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class TaxpayerDelegatePersistenceTests
{
    [Fact]
    public void Active_delegate_uniqueness_and_history_are_mapped()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options;
        using var db = new ArginTaxPayerDbContext(options);
        var model = db.Model.FindEntityType(typeof(TaxpayerDelegateGrant))!;
        Assert.Equal("taxpayer_delegate_grants", model.GetTableName());
        Assert.Contains(model.GetIndexes(), x => x.IsUnique &&
            x.GetFilter() == "revoked_at_utc IS NULL" &&
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TaxpayerId", "DelegateUserId" }));
        Assert.Equal(4, model.GetForeignKeys().Count());
        Assert.All(model.GetForeignKeys(), x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
    }

    [Fact]
    public void Revocation_preserves_actor_and_reason_columns()
    {
        var t = typeof(TaxpayerDelegateGrant);
        Assert.NotNull(t.GetProperty("RevokedAtUtc"));
        Assert.NotNull(t.GetProperty("RevokedByLevel1UserId"));
        Assert.NotNull(t.GetProperty("RevocationReason"));
        Assert.NotNull(t.GetProperty("Revision"));
    }
}
