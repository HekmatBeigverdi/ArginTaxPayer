using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArginTaxPayer.Application.Signing;

[JsonConverter(typeof(SensitiveKeyInputJsonConverter))]
public sealed class SensitiveKeyInput : IDisposable
{
    private byte[]? bytes;
    private char[]? password;
    public SigningPrivateKeyFormat Format { get; }
    public SensitiveKeyInput(SigningPrivateKeyFormat format, ReadOnlySpan<byte> encodedKey,
        ReadOnlySpan<char> password = default)
    {
        if (!Enum.IsDefined(format) || encodedKey.Length is < 1 or > 65536 || password.Length > 1024)
            throw new ArgumentException("Invalid sensitive key input.");
        Format = format;
        bytes = encodedKey.ToArray();
        this.password = password.ToArray();
    }
    internal ReadOnlyMemory<byte> KeyBytes => bytes ?? throw new ObjectDisposedException(nameof(SensitiveKeyInput));
    internal ReadOnlyMemory<char> Password => password ?? throw new ObjectDisposedException(nameof(SensitiveKeyInput));
    public override string ToString() => "[sensitive key input]";
    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref bytes, null);
        var secret = Interlocked.Exchange(ref password, null);
        if (owned is not null) CryptographicOperations.ZeroMemory(owned);
        if (secret is not null) CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(secret.AsSpan()));
    }
}

public sealed class SensitiveKeyInputJsonConverter : JsonConverter<SensitiveKeyInput>
{
    public override SensitiveKeyInput? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Sensitive key input cannot be serialized.");
    public override void Write(Utf8JsonWriter writer, SensitiveKeyInput value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Sensitive key input cannot be serialized.");
}
