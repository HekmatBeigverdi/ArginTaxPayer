using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class SharedProductionFiscalSerialRegistryTests
{
    [Fact]
    public async Task Separate_clients_share_serials_and_replays_do_not_allocate_again()
    {
        var raw = Environment.GetEnvironmentVariable("PHASE18_PRODUCER_TEST_CONNECTION");
        if (string.IsNullOrEmpty(raw)) return;
        var settings = new NpgsqlConnectionStringBuilder(raw);
        if (settings.Host is not ("localhost" or "127.0.0.1") || settings.Database != "argintaxpayer_test")
            throw new InvalidOperationException("Only loopback test DB permitted.");
        var schema = "shared_serial_" + Guid.NewGuid().ToString("N");
        await using var db = new NpgsqlConnection(raw);
        await db.OpenAsync();
        async Task Execute(string sql) { await using var c = new NpgsqlCommand(sql, db); await c.ExecuteNonQueryAsync(); }
        await Execute($"CREATE SCHEMA {schema}; SET search_path TO {schema}");
        settings.SearchPath = schema;
        try
        {
            await Execute("CREATE TABLE fiscal_memories(id uuid,memory_identifier text); CREATE TABLE authority_invoice_fiscal_reservations(fiscal_memory_id uuid,serial bigint,authority_environment int)");
            foreach (var sql in new AddSharedProductionFiscalSerials().UpOperations.OfType<SqlOperation>()) await Execute(sql.Sql);
            StoredInvoice Invoice() => new() { Id=Guid.NewGuid(),CustomerUserId=Guid.NewGuid(),SnapshotSha256=new string('a',64),IssueDateUtc=new DateTime(2026,11,15,10,0,0,DateTimeKind.Utc) };
            await Execute("INSERT INTO production_shared_serial_floors VALUES ('ABC123',13)");
            var first=Invoice();
            var a=await SharedProductionFiscalSerialRegistry.ReserveAsync(settings.ConnectionString,first,"ABC123",0,default);
            Assert.Equal(14,a);
            Assert.Equal(a,await SharedProductionFiscalSerialRegistry.ReserveAsync(settings.ConnectionString,first,"ABC123",99,default));
            var simultaneous=await Task.WhenAll(Enumerable.Range(0,6).Select(_ => SharedProductionFiscalSerialRegistry.ReserveAsync(settings.ConnectionString,Invoice(),"ABC123",0,default)));
            Assert.Equal(Enumerable.Range(15,6).Select(x=>(long)x),simultaneous.Order());
            first.SnapshotSha256=new string('b',64);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>SharedProductionFiscalSerialRegistry.ReserveAsync(settings.ConnectionString,first,"ABC123",0,default));
            await Assert.ThrowsAsync<PostgresException>(()=>Execute("DELETE FROM production_shared_serial_reservations"));
        }
        finally { NpgsqlConnection.ClearAllPools(); await Execute($"SET search_path TO public; DROP SCHEMA {schema} CASCADE"); }
    }
}
