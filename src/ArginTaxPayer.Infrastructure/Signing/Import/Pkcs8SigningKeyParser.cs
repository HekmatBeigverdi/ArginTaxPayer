using System.Formats.Asn1;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Import;

internal sealed class Pkcs8SigningKeyParser
{
    public GeneratedRsaKey Parse(SensitiveKeyInput input)
    {
        byte[]? decoded = null;
        byte[]? canonical = null;
        byte[]? derivedKey = null;
        byte[]? decrypted = null;
        try
        {
            var encrypted = input.Format is SigningPrivateKeyFormat.EncryptedPkcs8Der or SigningPrivateKeyFormat.EncryptedPkcs8Pem;
            if (encrypted == input.Password.IsEmpty)
                throw new KeyImportException(KeyImportFailure.InvalidKey);
            ReadOnlyMemory<byte> encoded = input.KeyBytes;
            if (input.Format is SigningPrivateKeyFormat.Pkcs8Pem or SigningPrivateKeyFormat.EncryptedPkcs8Pem)
                encoded = decoded = DecodePem(encoded, encrypted ? "ENCRYPTED PRIVATE KEY" : "PRIVATE KEY");
            using var rsa = RSA.Create();
            ReadOnlyMemory<byte> keyDer = encoded;
            if (encrypted)
            {
                var protection = Pbes2ImportPolicy.Parse(encoded); // Bound KDF cost before decryption.
                derivedKey = Rfc2898DeriveBytes.Pbkdf2(input.Password.Span, protection.Salt,
                    protection.Iterations, HashAlgorithmName.SHA256, 32);
                using var aes = Aes.Create();
                aes.Key = derivedKey;
                keyDer = decrypted = aes.DecryptCbc(protection.Ciphertext, protection.Iv, PaddingMode.PKCS7);
            }
            if (keyDer.Length > KeyEnvelopeProtector.MaximumKeyBytes)
                throw new KeyImportException(KeyImportFailure.InvalidKey);
            ValidatePlainDer(keyDer);
            rsa.ImportPkcs8PrivateKey(keyDer.Span, out var consumed);
            if (consumed != keyDer.Length) throw new KeyImportException(KeyImportFailure.InvalidKey);
            RsaSigningKeyMaterial.ValidateStrength(rsa);
            var challenge = RandomNumberGenerator.GetBytes(32);
            var signature = rsa.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!rsa.VerifyData(challenge, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new KeyImportException(KeyImportFailure.InvalidKey);
            canonical = rsa.ExportPkcs8PrivateKey();
            // Also validates canonical PKCS8 and exact supported strength with the shared local profile.
            var publicBytes = RsaSigningKeyMaterial.DerivePublic(canonical);
            var result = new GeneratedRsaKey(SensitiveBytes.TakeOwnership(canonical), publicBytes);
            canonical = null;
            return result;
        }
        catch (Exception error) when (error is CryptographicException or AsnContentException or
            FormatException or ArgumentException or KeyProtectionException or ObjectDisposedException)
        { throw new KeyImportException(KeyImportFailure.InvalidKey); }
        finally
        {
            if (derivedKey is not null) CryptographicOperations.ZeroMemory(derivedKey);
            if (decrypted is not null) CryptographicOperations.ZeroMemory(decrypted);
            if (decoded is not null) CryptographicOperations.ZeroMemory(decoded);
            if (canonical is not null) CryptographicOperations.ZeroMemory(canonical);
        }
    }

    private static byte[] DecodePem(ReadOnlyMemory<byte> bytes, string label)
    {
        var chars = new char[bytes.Length];
        byte[]? decoded = null;
        try
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                var value = bytes.Span[i];
                if (value > 127) throw new KeyImportException(KeyImportFailure.InvalidKey);
                chars[i] = (char)value;
            }
            if (!PemEncoding.TryFind(chars, out var fields) ||
                !chars.AsSpan()[fields.Label].SequenceEqual(label.AsSpan()))
                throw new KeyImportException(KeyImportFailure.InvalidKey);
            var (start, length) = fields.Location.GetOffsetAndLength(chars.Length);
            if (!Whitespace(chars.AsSpan(0, start)) || !Whitespace(chars.AsSpan(start + length)) ||
                fields.DecodedDataLength > 65536)
                throw new KeyImportException(KeyImportFailure.InvalidKey);
            decoded = new byte[fields.DecodedDataLength];
            if (!Convert.TryFromBase64Chars(chars.AsSpan()[fields.Base64Data], decoded, out var count) ||
                count != decoded.Length)
                throw new KeyImportException(KeyImportFailure.InvalidKey);
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

    private static bool Whitespace(ReadOnlySpan<char> value)
    {
        foreach (var c in value) if (c is not (' ' or '\t' or '\r' or '\n')) return false;
        return true;
    }

    private static void ValidatePlainDer(ReadOnlyMemory<byte> encoded)
    {
        byte[]? privatePart = null;
        try
        {
            var root = new AsnReader(encoded, AsnEncodingRules.DER);
            var sequence = root.ReadSequence();
            if (!sequence.TryReadInt32(out var version) || version != 0)
                throw new KeyImportException(KeyImportFailure.InvalidKey);
            var algorithm = sequence.ReadSequence();
            if (algorithm.ReadObjectIdentifier() != "1.2.840.113549.1.1.1")
                throw new KeyImportException(KeyImportFailure.InvalidKey);
            if (algorithm.HasData) algorithm.ReadNull();
            algorithm.ThrowIfNotEmpty();
            privatePart = sequence.ReadOctetString();
            sequence.ThrowIfNotEmpty();
            root.ThrowIfNotEmpty();
            var keyReader = new AsnReader(privatePart, AsnEncodingRules.DER);
            var rsa = keyReader.ReadSequence();
            if (!rsa.TryReadInt32(out var rsaVersion) || rsaVersion != 0)
                throw new KeyImportException(KeyImportFailure.InvalidKey);
            for (var i = 0; i < 8; i++)
                if (rsa.ReadIntegerBytes().Span[0] >= 128)
                    throw new KeyImportException(KeyImportFailure.InvalidKey);
            rsa.ThrowIfNotEmpty();
            keyReader.ThrowIfNotEmpty();
        }
        finally { if (privatePart is not null) CryptographicOperations.ZeroMemory(privatePart); }
    }
}
