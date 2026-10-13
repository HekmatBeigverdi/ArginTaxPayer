using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ArginTaxPayer.Infrastructure.Persistence.Identity;

/// <summary>RFC 6238 SHA1 / 6-digit / 30-second authenticator interoperability.</summary>
public static class IdentityTotp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string NewSecret() => ToBase32(RandomNumberGenerator.GetBytes(20));

    public static string Uri(string email, string secret)
    {
        return "otpauth://totp/ArginTaxPayer:" + UriEscape(email)
            + "?secret=" + secret + "&issuer=ArginTaxPayer&algorithm=SHA1&digits=6&period=30";
    }

    private static string UriEscape(string value) => System.Uri.EscapeDataString(value);

    public static string Generate(string secret, DateTime utc)
    {
        var key = DecodeBase32(secret);
        var step = new DateTimeOffset(utc.ToUniversalTime()).ToUnixTimeSeconds() / 30;
        return GenerateAtStep(key, step);
    }

    public static long? MatchStep(string secret, string? code, DateTime utc, long lastAcceptedStep)
    {
        if (code is not { Length: 6 } || code.Any(c => c < '0' || c > '9'))
            return null;
        var key = DecodeBase32(secret);
        var now = new DateTimeOffset(utc.ToUniversalTime()).ToUnixTimeSeconds() / 30;
        for (var offset = -1; offset <= 1; offset++)
        {
            var step = now + offset;
            if (step <= lastAcceptedStep || step < 0) continue;
            var candidate = GenerateAtStep(key, step);
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(code)))
                return step;
        }
        return null;
    }

    private static string GenerateAtStep(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hmac = HMACSHA1.HashData(key, counter);
        var offset = hmac[^1] & 15;
        var number = ((hmac[offset] & 127) << 24) | ((hmac[offset + 1] & 255) << 16)
            | ((hmac[offset + 2] & 255) << 8) | (hmac[offset + 3] & 255);
        return (number % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ToBase32(byte[] bytes)
    {
        var sb = new StringBuilder();
        var buffer = 0;
        var bits = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[] DecodeBase32(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length > 128)
            throw new FormatException("Invalid TOTP secret.");
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var ch in input.TrimEnd('='))
        {
            var val = Alphabet.IndexOf(ch);
            if (val < 0) throw new FormatException("Invalid TOTP secret.");
            buffer = (buffer << 5) | val;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((buffer >> bits) & 255));
            }
        }
        if (bytes.Count != 20) throw new FormatException("Unexpected TOTP secret length.");
        return bytes.ToArray();
    }
}
