using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

/// <summary>
/// Encryption envelopes only; idempotent insert and optimistic CAS, no plaintext key columns.
/// All SQL access is bound to the five UUIDs of the immutable signing operation.
/// </summary>
internal sealed class DatabaseProtectedKeyEnvelopeStore(ArginTaxPayerDbContext db) : IProtectedKeyEnvelopeStore
{
    private IQueryable<DbSigningKeyEnvelope> Scoped(KeyProtectionIdentity id) =>
        db.DbSigningKeyEnvelopes.Where(x => x.KeyVersionId == id.KeyVersionId &&
            x.CustomerUserId == id.CustomerUserId && x.TaxpayerId == id.TaxpayerId &&
            x.FiscalMemoryId == id.FiscalMemoryId && x.OriginOperationRecordId == id.OperationRecordId);

    public async Task<StoredKeyEnvelope?> FindAsync(KeyProtectionIdentity id, CancellationToken ct)
    {
        id.Validate();
        var row = await Scoped(id).AsNoTracking().SingleOrDefaultAsync(ct);
        if (row is null) return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<ProtectedKeyEnvelope>(row.EnvelopeJson)
                ?? throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            KeyEnvelopeProtector.Validate(id, envelope);
            if (row.Version < 1 || row.Version == int.MaxValue ||
                envelope.ProtectionKeyVersion != DatabaseKeyProtectionProvider.KeyVersion)
                throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            return new(envelope, row.Version);
        }
        catch (JsonException) { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
    }

    public async Task<StoredKeyEnvelope> CreateAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope envelope, CancellationToken ct)
    {
        id.Validate();
        KeyEnvelopeProtector.Validate(id, envelope);
        if (envelope.ProtectionKeyVersion != DatabaseKeyProtectionProvider.KeyVersion)
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        var row = new DbSigningKeyEnvelope
        {
            KeyVersionId = id.KeyVersionId, CustomerUserId = id.CustomerUserId,
            TaxpayerId = id.TaxpayerId, FiscalMemoryId = id.FiscalMemoryId,
            OriginOperationRecordId = id.OperationRecordId,
            EnvelopeJson = JsonSerializer.Serialize(envelope), Version = 1
        };
        db.DbSigningKeyEnvelopes.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            return new(envelope, 1);
        }
        catch (DbUpdateException) { throw new KeyProtectionException(KeyProtectionFailure.Conflict); }
    }

    public async Task<StoredKeyEnvelope> ReplaceAsync(
        KeyProtectionIdentity id, ProtectedKeyEnvelope envelope, int expectedVersion, CancellationToken ct)
    {
        id.Validate();
        KeyEnvelopeProtector.Validate(id, envelope);
        if (expectedVersion is < 1 or >= int.MaxValue ||
            envelope.ProtectionKeyVersion != DatabaseKeyProtectionProvider.KeyVersion)
            throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);
        var json = JsonSerializer.Serialize(envelope);
        var count = await Scoped(id).Where(x => x.Version == expectedVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.EnvelopeJson, json)
                .SetProperty(x => x.Version, expectedVersion + 1), ct);
        if (count != 1) throw new KeyProtectionException(KeyProtectionFailure.Conflict);
        return new(envelope, expectedVersion + 1);
    }
}
