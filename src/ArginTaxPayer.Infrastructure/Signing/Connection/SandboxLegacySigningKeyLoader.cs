using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

/// <summary>
/// Loads only an encrypted Sandbox memory key; never consults fiscal_memories
/// or production signing-key selections. Returns a zeroizable key scope.
/// </summary>
public sealed class SandboxLegacySigningKeyLoader(
    ArginTaxPayerDbContext db, KeyEnvelopeProtector protector)
{
    internal async Task<DatabaseLegacySigningKey?> RecoverAsync(
        Guid customerUserId, Guid taxpayerId, Guid sandboxMemoryId,
        CancellationToken ct = default)
    {
        if (customerUserId == Guid.Empty || taxpayerId == Guid.Empty ||
            sandboxMemoryId == Guid.Empty)
            return null;
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
SELECT public_spki_der, private_envelope_json,
       signing_key_version_id, origin_operation_id
FROM sandbox_fiscal_memories
WHERE id=@memory AND customer_user_id=@owner AND taxpayer_id=@taxpayer
 AND private_envelope_json IS NOT NULL
""";
            foreach (var (name,value) in new (string,object)[] {
                ("memory",sandboxMemoryId),("owner",customerUserId),("taxpayer",taxpayerId)
            })
            {
                var p=command.CreateParameter();
                p.ParameterName=name;
                p.Value=value;
                command.Parameters.Add(p);
            }
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            var spki = (byte[])reader.GetValue(0);
            var envelopeJson = reader.GetString(1);
            var keyVersion = reader.GetGuid(2);
            var originOperation = reader.GetGuid(3);
            await reader.DisposeAsync();
            ProtectedKeyEnvelope? envelope;
            try { envelope = JsonSerializer.Deserialize<ProtectedKeyEnvelope>(envelopeJson); }
            catch (JsonException) { return null; }
            if (envelope is null) return null;
            var identity = new KeyProtectionIdentity(customerUserId,taxpayerId,
                sandboxMemoryId,keyVersion,originOperation,Sandbox:true);
            var secret = await protector.UnprotectAsync(identity,envelope,ct);
            var transferred=false;
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(secret.Bytes.Span,out var consumed);
                if (consumed!=secret.Bytes.Length) return null;
                var derived = rsa.ExportSubjectPublicKeyInfo();
                try
                {
                    if (!CryptographicOperations.FixedTimeEquals(spki,derived))
                        return null;
                }
                finally { CryptographicOperations.ZeroMemory(derived); }
                var result = new DatabaseLegacySigningKey(secret, (byte[])spki.Clone());
                transferred=true;
                return result;
            }
            finally { if (!transferred) secret.Dispose(); }
        }
        finally { if (openedHere) await connection.CloseAsync(); }
    }
}
