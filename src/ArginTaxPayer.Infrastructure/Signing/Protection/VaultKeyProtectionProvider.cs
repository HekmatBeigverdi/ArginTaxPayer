using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

internal sealed class VaultKeyProtectionProvider(VaultHttpTransport transport, VaultProtectionOptions options)
    : IKeyProtectionProvider
{
    private string Endpoint(string action) => $"v1/{options.TransitMount}/{action}/{options.TransitKey}";
    internal static string WrappingVersion(string ciphertext)
    {
        if (ciphertext is null || ciphertext.Length > 4096 ||
            !Regex.IsMatch(ciphertext, "^vault:v[1-9][0-9]*:[A-Za-z0-9+/]+={0,2}$", RegexOptions.CultureInvariant))
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        var version = ciphertext[..ciphertext.LastIndexOf(':')];
        if (!int.TryParse(version.AsSpan(7), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        return version;
    }

    public async Task<WrappedDataKey> WrapAsync(KeyProtectionIdentity id, ReadOnlyMemory<byte> dek, CancellationToken ct)
    {
        id.Validate();
        if (dek.Length != 32) throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);
        var copy = dek.ToArray();
        try
        {
            using var json = await transport.SendAsync(HttpMethod.Post, Endpoint("encrypt"),
                new { plaintext = copy, context = Convert.ToBase64String(id.TransitBinding()) }, false, false, ct);
            return Wrapped(json);
        }
        finally { CryptographicOperations.ZeroMemory(copy); }
    }

    public async Task<SensitiveBytes> UnwrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct)
    {
        id.Validate();
        if (WrappingVersion(wrapped.Ciphertext) != wrapped.ProtectionKeyVersion)
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        using var json = await transport.SendAsync(HttpMethod.Post, Endpoint("decrypt"),
            new { ciphertext = wrapped.Ciphertext,
                context = Convert.ToBase64String(id.TransitBinding()) }, false, false, ct);
        byte[]? bytes = null;
        try
        {
            bytes = json!.RootElement.GetProperty("data").GetProperty("plaintext").GetBytesFromBase64();
            if (bytes.Length != 32) throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            var result = SensitiveBytes.TakeOwnership(bytes);
            bytes = null;
            return result;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
        finally { if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); }
    }

    public async Task<WrappedDataKey> RewrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct)
    {
        id.Validate();
        if (WrappingVersion(wrapped.Ciphertext) != wrapped.ProtectionKeyVersion)
            throw new KeyProtectionException(KeyProtectionFailure.Integrity);
        using var json = await transport.SendAsync(HttpMethod.Post, Endpoint("rewrap"),
            new { ciphertext = wrapped.Ciphertext,
                context = Convert.ToBase64String(id.TransitBinding()) }, false, false, ct);
        return Wrapped(json);
    }

    private static WrappedDataKey Wrapped(JsonDocument? json)
    {
        try
        {
            var ciphertext = json!.RootElement.GetProperty("data").GetProperty("ciphertext").GetString()
                ?? throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            return new(ciphertext, WrappingVersion(ciphertext));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new KeyProtectionException(KeyProtectionFailure.Integrity); }
    }
}
