using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class VerifiedCollectionRetryPostgresTests
{
    [Fact]
    public async Task Manual_receipts_with_retry_enabled_reduce_balance_and_replay_without_duplicates()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test database required.");
        if (new NpgsqlConnectionStringBuilder(cs).Database != "argintaxpayer_test")
            throw new InvalidOperationException("Requires argintaxpayer_test.");
        // Isolate committed, append-only receipt evidence from application data.
        var schema = "receipt_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(cs);
        await admin.OpenAsync();
        await using (var create = admin.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA {schema}";
            await create.ExecuteNonQueryAsync();
            foreach (var table in new[] { "identity_users", "identity_user_roles", "identity_mfa_states",
                "identity_sessions", "customer_commercial_owners", "subscription_plan_catalog",
                "subscription_price_versions", "commercial_orders", "commercial_collections" })
            {
                create.CommandText = $"CREATE TABLE {schema}.{table} (LIKE public.{table} INCLUDING ALL)";
                await create.ExecuteNonQueryAsync();
            }
        }
        var isolated = new NpgsqlConnectionStringBuilder(cs) { SearchPath = schema }.ConnectionString;
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(isolated, p => p.EnableRetryOnFailure(3)).Options;
        await using var db = new ArginTaxPayerDbContext(options);
        var root = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var plan = Guid.NewGuid();
        var price = Guid.NewGuid();
        var order = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO identity_users(id,normalized_email,is_active,created_at_utc)
                VALUES ({root},{"receipt-root-" + root + "@example.invalid"},true,{now}),
                       ({customer},{"receipt-customer-" + customer + "@example.invalid"},true,{now});
                INSERT INTO identity_user_roles(user_id,role,assigned_at_utc) VALUES ({root},'Level1',{now});
                INSERT INTO identity_mfa_states(user_id,encrypted_secret,enabled_at_utc)
                VALUES ({root},'fixture-only',{now});
                INSERT INTO identity_sessions(id,user_id,access_token_hash,refresh_token_hash,
                    access_expires_at_utc,refresh_expires_at_utc,created_at_utc)
                VALUES ({Guid.NewGuid()},{root},{digest},{Guid.NewGuid().ToString("N")},
                    {now.AddHours(1)},{now.AddDays(1)},{now});
                INSERT INTO subscription_plan_catalog(id,code,kind,validity_days,is_active,created_at_utc)
                VALUES ({plan},{"receipt-" + plan},1,365,true,{now});
                INSERT INTO subscription_price_versions(id,plan_id,root_admin_user_id,audience,
                    price_irr,effective_from_utc,created_at_utc)
                VALUES ({price},{plan},{root},1,1000,{now},{now});
                INSERT INTO commercial_orders(id,customer_user_id,plan_id,price_version_id,
                    root_admin_user_id,idempotency_key,gross_amount_irr,collected_amount_irr,status,issued_at_utc)
                VALUES ({order},{customer},{plan},{price},{root},{order.ToString("N")},1000,0,1,{now});
                """);
            var service = new VerifiedCollectionService(db,
                new IdentityAuthorizationService(db, new IdentitySessionService(db)));
            var receipt = new VerifiedCollection(order, customer, CollectionChannel.Cash,
                400, "receipt-" + Guid.NewGuid(), now);
            var partial = await service.RecordManualAsync(token, receipt);
            Assert.Equal(new ReceivableBalance(1000, 400), partial);
            Assert.Equal(partial, await service.RecordManualAsync(token, receipt));
            var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordManualAsync(token,
                receipt with { AmountIrr = 500 }));
            Assert.Equal("Payment reference reused with different details.", conflict.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordManualAsync(token,
                receipt with { AmountIrr = 601, ExternalReference = "over-" + Guid.NewGuid() }));
            var final = await service.RecordManualAsync(token, receipt with
            {
                Channel = CollectionChannel.BankTransfer, AmountIrr = 600,
                ExternalReference = "bank-" + Guid.NewGuid()
            });
            Assert.Equal(new ReceivableBalance(1000, 1000), final);
            Assert.Equal(2, await db.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM commercial_collections WHERE order_id={0}", order).SingleAsync());
            Assert.Equal(1000L, await db.Database.SqlQueryRaw<long>(
                "SELECT collected_amount_irr AS \"Value\" FROM commercial_orders WHERE id={0}", order).SingleAsync());
        }
        finally
        {
            await using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP SCHEMA {schema} CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
