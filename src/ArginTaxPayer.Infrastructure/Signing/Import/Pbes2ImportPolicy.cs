using System.Formats.Asn1;

namespace ArginTaxPayer.Infrastructure.Signing.Import;

internal enum KeyImportFailure { InvalidKey, UnsupportedProtection }
internal sealed class KeyImportException(KeyImportFailure failure) : Exception("Private key input rejected.")
{
    public KeyImportFailure Failure { get; } = failure;
}

internal sealed record Pbes2Parameters(byte[] Salt, int Iterations, byte[] Iv, byte[] Ciphertext);

internal static class Pbes2ImportPolicy
{
    public static Pbes2Parameters Parse(ReadOnlyMemory<byte> encoded)
    {
        var root = new AsnReader(encoded, AsnEncodingRules.DER);
        var outer = root.ReadSequence();
        var algorithm = outer.ReadSequence();
        Require(algorithm.ReadObjectIdentifier() == "1.2.840.113549.1.5.13");
        var pbes2 = algorithm.ReadSequence();
        var kdf = pbes2.ReadSequence();
        Require(kdf.ReadObjectIdentifier() == "1.2.840.113549.1.5.12");
        var parameters = kdf.ReadSequence();
        var salt = parameters.ReadOctetString();
        Require(salt.Length is >= 8 and <= 64);
        Require(parameters.TryReadInt32(out var iterations) && iterations is >= 1 and <= 600000);
        if (parameters.HasData && parameters.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
            Require(parameters.TryReadInt32(out var keyLength) && keyLength == 32);
        Require(parameters.HasData); // No implicit/default SHA1.
        var prf = parameters.ReadSequence();
        Require(prf.ReadObjectIdentifier() == "1.2.840.113549.2.9");
        if (prf.HasData) prf.ReadNull();
        prf.ThrowIfNotEmpty();
        parameters.ThrowIfNotEmpty();
        kdf.ThrowIfNotEmpty();
        var cipher = pbes2.ReadSequence();
        Require(cipher.ReadObjectIdentifier() == "2.16.840.1.101.3.4.1.42");
        var iv = cipher.ReadOctetString();
        Require(iv.Length == 16);
        cipher.ThrowIfNotEmpty();
        pbes2.ThrowIfNotEmpty();
        algorithm.ThrowIfNotEmpty();
        var ciphertext = outer.ReadOctetString();
        Require(ciphertext.Length is >= 16 and <= 16400 && ciphertext.Length % 16 == 0);
        outer.ThrowIfNotEmpty();
        root.ThrowIfNotEmpty();
        return new Pbes2Parameters(salt, iterations, iv, ciphertext);
    }

    private static void Require(bool valid)
    {
        if (!valid) throw new KeyImportException(KeyImportFailure.UnsupportedProtection);
    }
}
