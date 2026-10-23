using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Matching;

internal sealed class SigningPublicKeyParser
{
    public byte[] Parse(SigningPublicKeyFormat format, ReadOnlyMemory<byte> input)
    {
        byte[]? decoded = null;
        try
        {
            if (!Enum.IsDefined(format) || input.Length is < 1 or > 16384) throw Invalid();
            var spki = format is SigningPublicKeyFormat.SpkiDer or SigningPublicKeyFormat.SpkiPem;
            if (format is SigningPublicKeyFormat.SpkiPem or SigningPublicKeyFormat.RsaPublicPem)
                input = decoded = Decode(input, spki ? "PUBLIC KEY" : "RSA PUBLIC KEY");
            if (input.Length is < 1 or > 4096) throw Invalid();
            using var rsa = RSA.Create();
            int consumed;
            if (spki) rsa.ImportSubjectPublicKeyInfo(input.Span, out consumed);
            else rsa.ImportRSAPublicKey(input.Span, out consumed);
            RsaSigningKeyMaterial.ValidateStrength(rsa);
            // Re-export equality rejects BER, trailing bytes and noncanonical DER.
            var canonicalInput = spki ? rsa.ExportSubjectPublicKeyInfo() : rsa.ExportRSAPublicKey();
            if (consumed != input.Length || !canonicalInput.AsSpan().SequenceEqual(input.Span)) throw Invalid();
            return rsa.ExportSubjectPublicKeyInfo();
        }
        catch (Exception error) when (error is CryptographicException or ArgumentException or FormatException)
        { throw Invalid(); }
        finally { if (decoded is not null) CryptographicOperations.ZeroMemory(decoded); }
    }

    private static KeyProtectionException Invalid() => new(KeyProtectionFailure.InvalidInput);
    private static byte[] Decode(ReadOnlyMemory<byte> bytes, string label)
    {
        var chars = new char[bytes.Length];
        byte[]? decoded = null;
        try
        {
            for (var i = 0; i < chars.Length; i++)
            {
                if (bytes.Span[i] > 127) throw Invalid();
                chars[i] = (char)bytes.Span[i];
            }
            if (!PemEncoding.TryFind(chars, out var fields) ||
                !chars.AsSpan()[fields.Label].SequenceEqual(label.AsSpan())) throw Invalid();
            var (start, length) = fields.Location.GetOffsetAndLength(chars.Length);
            if (!Whitespace(chars.AsSpan(0, start)) || !Whitespace(chars.AsSpan(start + length)) ||
                fields.DecodedDataLength is < 1 or > 4096) throw Invalid();
            decoded = new byte[fields.DecodedDataLength];
            if (!Convert.TryFromBase64Chars(chars.AsSpan()[fields.Base64Data], decoded, out var used) ||
                used != decoded.Length) throw Invalid();
            var result = decoded;
            decoded = null;
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(chars.AsSpan()));
            if (decoded is not null) CryptographicOperations.ZeroMemory(decoded);
        }
    }
    private static bool Whitespace(ReadOnlySpan<char> input)
    {
        foreach (var c in input) if (c is not (' ' or '\t' or '\r' or '\n')) return false;
        return true;
    }
}
