using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class SubscriptionAdjustmentPostgresTests
{
    [SubscriptionAdjustmentPostgresFact]
    public async Task Level1_adjusts_quantity_and_days_with_database_retries_enabled()
    {
        var connection = Environment.GetEnvironmentVariable("ARGIN_ADJUSTMENT_TEST_DB")!;
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(connection).Database);
        await using var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(connection, options => options.EnableRetryOnFailure(3)).Options);
        await db.Database.MigrateAsync();

        var now = DateTime.UtcNow;
        var admin = new IdentityUser { Id = Guid.NewGuid(), CreatedAtUtc = now, NormalizedEmail = Guid.NewGuid() + "@example.test" };
        var customer = new IdentityUser { Id = Guid.NewGuid(), CreatedAtUtc = now, NormalizedEmail = Guid.NewGuid() + "@example.test" };
        db.IdentityUsers.AddRange(admin, customer);
        db.IdentityUserRoles.Add(new() { UserId = admin.Id, Role = IdentityRoleNames.Level1, AssignedAtUtc = now });
        db.IdentityMfaStates.Add(new() { UserId = admin.Id, EnabledAtUtc = now, EncryptedSecret = "test" });

        var plan = new StoredSubscriptionPlan
        {
            Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Kind = SubscriptionPlanKind.Quantity,
            ValidityDays = 365, InvoiceUnitLimit = 300, IsActive = true, CreatedAtUtc = now
        };
        var price = new StoredSubscriptionPrice
        {
            Id = Guid.NewGuid(), PlanId = plan.Id, RootAdminUserId = admin.Id, Audience = PriceAudience.RootDirect,
            PriceIrr = 1000, EffectiveFromUtc = now, CreatedAtUtc = now
        };
        var subscription = new StoredCustomerSubscription
        {
            Id = Guid.NewGuid(), CustomerUserId = customer.Id, PlanId = plan.Id, PriceVersionId = price.Id,
            RootAdminUserId = admin.Id, Kind = plan.Kind, State = SubscriptionLifecycle.Active,
            ValidityDays = 365, InvoiceUnitLimit = 300, PriceIrr = price.PriceIrr,
            PurchasedAtUtc = now, ActivatedAtUtc = now, ExpiresAtUtc = now.AddDays(365)
        };
        db.SubscriptionPlans.Add(plan);
        db.SubscriptionPrices.Add(price);
        db.CustomerSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO subscription_credit_accounts
                (subscription_id,customer_user_id,granted_units,available_units,reserved_units,consumed_units,revision)
            VALUES ({subscription.Id},{customer.Id},300,300,0,0,1)
            """);

        var sessions = new IdentitySessionService(db);
        var tokens = await sessions.CreateMfaSessionAsync(admin.Id);
        Assert.NotNull(tokens);
        var service = new SubscriptionAdjustmentService(db, new IdentityAuthorizationService(db, sessions));
        var operation = Guid.NewGuid().ToString("N");
        var baseline = await service.ReadActiveAsync(tokens.AccessToken, customer.Id);
        Assert.NotNull(baseline);

        var updated = await service.AdjustAsync(tokens.AccessToken, customer.Id, subscription.Id,
            40, 291, baseline.Revision, operation, "Transfer plan");
        Assert.Equal(291, updated.InvoiceUnitLimit);
        Assert.Equal(291, updated.AvailableUnits);
        Assert.Equal(40, updated.ValidityDays);
        Assert.InRange(updated.ExpiresAtUtc!.Value, now.AddDays(40), DateTime.UtcNow.AddDays(40));
        Assert.Equal(baseline.Revision + 1, updated.Revision);

        var replay = await service.AdjustAsync(tokens.AccessToken, customer.Id, subscription.Id,
            40, 291, baseline.Revision, operation, "Transfer plan");
        Assert.Equal(updated, replay);
        Assert.Single(await service.ReadHistoryAsync(tokens.AccessToken, customer.Id));
        Assert.Equal(updated, await service.ReadActiveAsync(tokens.AccessToken, customer.Id));
    }
}

internal sealed class SubscriptionAdjustmentPostgresFactAttribute : FactAttribute
{
    public SubscriptionAdjustmentPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARGIN_ADJUSTMENT_TEST_DB")))
            Skip = "Requires a disposable argintaxpayer_test PostgreSQL database.";
    }
}
