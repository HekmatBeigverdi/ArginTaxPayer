using System.Text.Json;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

internal sealed class VaultProtectedKeyEnvelopeStore(VaultHttpTransport transport, VaultProtectionOptions options)
    : IProtectedKeyEnvelopeStore
{
    private string Endpoint(KeyProtectionIdentity id) => $"v1/{options.KvMount}/data/{id.StoragePath()}";

    public async Task<StoredKeyEnvelope?> FindAsync(KeyProtectionIdentity id, CancellationToken ct)
    {
        id.Validate();
        using var json = await transport.SendAsync(HttpMethod.Get, Endpoint(id), null, true, false, ct);
        if (json is null) return null;
        try
        {
            var data = json.RootElement.GetProperty("data");
            var metadata = data.GetProperty("metadata");
            if (metadata.TryGetProperty("destroyed", out var destroyed) && destroyed.GetBoolean() ||
                metadata.TryGetProperty("deletion_time", out var deletion) && !string.IsNullOrEmpty(deletion.GetString()))
                throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            var envelope = data.GetProperty("data").Deserialize<ProtectedKeyEnvelope>()
                ?? throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            KeyEnvelopeProtector.Validate(id, envelope);
            var version = metadata.GetProperty("version").GetInt32();
            if (version < 1) throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            return new(envelope, version);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
    }

    public Task<StoredKeyEnvelope> CreateAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope envelope, CancellationToken ct) =>
        WriteAsync(id, envelope, 0, ct);

    public Task<StoredKeyEnvelope> ReplaceAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope envelope,
        int expectedVersion, CancellationToken ct)
    {
        if (expectedVersion < 1 || expectedVersion == int.MaxValue)
            throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);
        return WriteAsync(id, envelope, expectedVersion, ct);
    }

    private async Task<StoredKeyEnvelope> WriteAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope envelope,
        int cas, CancellationToken ct)
    {
        KeyEnvelopeProtector.Validate(id, envelope);
        using var json = await transport.SendAsync(HttpMethod.Post, Endpoint(id),
            new { options = new { cas }, data = envelope }, false, true, ct);
        try
        {
            var version = json!.RootElement.GetProperty("data").GetProperty("version").GetInt32();
            if (version != cas + 1) throw new KeyProtectionException(KeyProtectionFailure.RecoveryRequired);
            return new(envelope, version);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new KeyProtectionException(KeyProtectionFailure.RecoveryRequired); }
    }
}
