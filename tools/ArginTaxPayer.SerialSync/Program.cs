using Npgsql;

// Run once before enabling the new sender. Never prints connection credentials.
var sourceString = Environment.GetEnvironmentVariable("SERIAL_SYNC_SOURCE") ?? throw new InvalidOperationException("SERIAL_SYNC_SOURCE required (TEST DB).");
var targetString = Environment.GetEnvironmentVariable("SERIAL_SYNC_TARGET") ?? throw new InvalidOperationException("SERIAL_SYNC_TARGET required (Production migrator).");
if (new NpgsqlConnectionStringBuilder(sourceString).Database != "argintaxpayer_test" ||
    new NpgsqlConnectionStringBuilder(targetString).Database != "argintaxpayer_prod")
    throw new InvalidOperationException("Expected TEST source and Production target.");
await using var source = new NpgsqlConnection(sourceString);
await using var target = new NpgsqlConnection(targetString);
await source.OpenAsync();
await target.OpenAsync();
await using var read = new NpgsqlCommand("""
SELECT m.memory_identifier,max(r.serial) FROM authority_invoice_fiscal_reservations r
JOIN fiscal_memories m ON m.id=r.fiscal_memory_id
WHERE r.authority_environment=1 GROUP BY m.memory_identifier
""", source);
await using var reader = await read.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var code=reader.GetString(0); var serial=reader.GetInt64(1);
    await using var tx=await target.BeginTransactionAsync();
    await using var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@memory,1801))",target,tx);
    gate.Parameters.AddWithValue("memory",code);
    await gate.ExecuteNonQueryAsync();
    await using var update = new NpgsqlCommand("""
INSERT INTO production_shared_serial_floors(memory_code,last_serial)
SELECT @code,@serial WHERE EXISTS (
 SELECT 1 FROM fiscal_memories WHERE memory_identifier=@code AND authority_environment=1)
ON CONFLICT(memory_code) DO UPDATE
SET last_serial=GREATEST(production_shared_serial_floors.last_serial,EXCLUDED.last_serial)
""", target,tx);
    update.Parameters.AddWithValue("code",code);
    update.Parameters.AddWithValue("serial",serial);
    var count=await update.ExecuteNonQueryAsync();
    await tx.CommitAsync();
    Console.WriteLine($"Memory …{code[^2..]}: {(count==1 ? "floor synchronized" : "no matching Production memory")}; source maximum={serial}");
}
