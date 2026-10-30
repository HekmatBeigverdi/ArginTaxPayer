using System.Security.Cryptography;
using System.Text;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

// Internal custody boundary. None of these types is an API DTO or a business DB entity.
internal readonly record struct KeyProtectionIdentity(
    Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId, Guid KeyVersionId, Guid OperationRecordId,
    bool Sandbox = false)
{
    public void Validate()
    {
        if (CustomerUserId == Guid.Empty || TaxpayerId == Guid.Empty || FiscalMemoryId == Guid.Empty ||
            KeyVersionId == Guid.Empty || OperationRecordId == Guid.Empty)
            throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);
    }

    public byte[] Aad()
    {
        Validate();
        return Encoding.ASCII.GetBytes(
            $"{(Sandbox ? "argin:key:sandbox:v1" : "argin:key:v1")}|{CustomerUserId:D}|{TaxpayerId:D}|{FiscalMemoryId:D}|{KeyVersionId:D}");
    }

    // Transit binds the originating journal operation as well as the mandated local GCM AAD.
    public byte[] TransitBinding() =>
        Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(Aad()) + $"|{OperationRecordId:D}");

    public string StoragePath()
    {
        Validate();
        return Sandbox
            ? $"keys/sandbox/{CustomerUserId:N}/{TaxpayerId:N}/{FiscalMemoryId:N}/{OperationRecordId:N}/{KeyVersionId:N}"
            : $"keys/{CustomerUserId:N}/{TaxpayerId:N}/{FiscalMemoryId:N}/{OperationRecordId:N}/{KeyVersionId:N}";
    }
}

internal enum KeyProtectionFailure { InvalidInput, Integrity, Unavailable, Conflict, RecoveryRequired }

internal sealed class KeyProtectionException : Exception
{
    public KeyProtectionFailure Failure { get; }
    public KeyProtectionException(KeyProtectionFailure failure) : base("Key protection failed: " + failure)
        => Failure = failure;
}

internal sealed class SensitiveBytes : IDisposable
{
    private byte[]? bytes;
    private SensitiveBytes(byte[] owned) => bytes = owned;
    public static SensitiveBytes TakeOwnership(byte[] owned) => new(owned);
    public ReadOnlyMemory<byte> Bytes => bytes ?? throw new ObjectDisposedException(nameof(SensitiveBytes));
    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref bytes, null);
        if (owned is not null) CryptographicOperations.ZeroMemory(owned);
    }
    public override string ToString() => "[protected material]";
}

internal sealed record WrappedDataKey(string Ciphertext, string ProtectionKeyVersion)
{
    public override string ToString() => "[wrapped data key]";
}

internal sealed record ProtectedKeyEnvelope(
    int FormatVersion, string AlgorithmId, KeyProtectionIdentity Identity,
    byte[] Nonce, byte[] Ciphertext, byte[] Tag, string WrappedDek, string ProtectionKeyVersion)
{
    public override string ToString() => "[protected key envelope]";
}

internal interface IKeyProtectionProvider
{
    Task<WrappedDataKey> WrapAsync(KeyProtectionIdentity id, ReadOnlyMemory<byte> dek, CancellationToken ct);
    Task<SensitiveBytes> UnwrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct);
    Task<WrappedDataKey> RewrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct);
}

internal sealed class UnavailableKeyProtectionProvider : IKeyProtectionProvider
{
    public Task<WrappedDataKey> WrapAsync(KeyProtectionIdentity id, ReadOnlyMemory<byte> dek, CancellationToken ct) =>
        Task.FromException<WrappedDataKey>(new KeyProtectionException(KeyProtectionFailure.Unavailable));
    public Task<SensitiveBytes> UnwrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct) =>
        Task.FromException<SensitiveBytes>(new KeyProtectionException(KeyProtectionFailure.Unavailable));
    public Task<WrappedDataKey> RewrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct) =>
        Task.FromException<WrappedDataKey>(new KeyProtectionException(KeyProtectionFailure.Unavailable));
}

internal sealed record StoredKeyEnvelope(ProtectedKeyEnvelope Envelope, int Version);

internal interface IProtectedKeyEnvelopeStore
{
    Task<StoredKeyEnvelope?> FindAsync(KeyProtectionIdentity id, CancellationToken ct);
    Task<StoredKeyEnvelope> CreateAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope envelope, CancellationToken ct);
    Task<StoredKeyEnvelope> ReplaceAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope envelope,
        int expectedVersion, CancellationToken ct);
}
