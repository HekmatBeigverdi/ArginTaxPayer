using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ArginTaxPayer.Workers.DatabaseBackup;

/// <summary>
/// Authenticated, chunked AES-256-GCM stream format. Each chunk and the terminal
/// frame are independently authenticated, so truncation cannot look successful.
/// </summary>
public static class BackupEncryption
{
    public const int ChunkSize = 1024 * 1024;
    public const int HeaderSize = 16;
    private const int KeySize = 32;
    private const int NoncePrefixSize = 8;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MagicSize = 8;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ARDBKP01");
    private static readonly byte[] EndMarker = Encoding.ASCII.GetBytes("END1");

    public static async Task<long> EncryptAsync(
        Stream plaintext,
        Stream encrypted,
        ReadOnlyMemory<byte> key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(encrypted);
        ValidateKey(key.Span);

        var header = new byte[HeaderSize];
        Magic.CopyTo(header, 0);
        RandomNumberGenerator.Fill(header.AsSpan(Magic.Length));
        await encrypted.WriteAsync(header, cancellationToken);

        var input = new byte[ChunkSize];
        var output = new byte[ChunkSize];
        var tag = new byte[TagSize];
        uint counter = 0;
        long totalPlaintextBytes = 0;
        using var aes = new AesGcm(key.Span, TagSize);

        while (true)
        {
            var count = await plaintext.ReadAsync(input.AsMemory(), cancellationToken);
            if (count == 0)
                break;

            var length = Int32Bytes(count);
            var nonce = CreateNonce(header, counter);
            var associatedData = CreateAssociatedData(header, counter, count);
            aes.Encrypt(nonce, input.AsSpan(0, count), output.AsSpan(0, count), tag, associatedData);
            await encrypted.WriteAsync(length, cancellationToken);
            await encrypted.WriteAsync(output.AsMemory(0, count), cancellationToken);
            await encrypted.WriteAsync(tag, cancellationToken);
            totalPlaintextBytes = checked(totalPlaintextBytes + count);
            counter = checked(counter + 1);
        }

        if (totalPlaintextBytes == 0)
            throw new InvalidDataException("Refusing to encrypt an empty database archive.");

        var terminalLength = Int32Bytes(0);
        var terminalNonce = CreateNonce(header, counter);
        var terminalAad = CreateTerminalAssociatedData(header, counter, totalPlaintextBytes);
        aes.Encrypt(terminalNonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, tag, terminalAad);
        await encrypted.WriteAsync(terminalLength, cancellationToken);
        await encrypted.WriteAsync(tag, cancellationToken);
        return totalPlaintextBytes;
    }

    public static async Task<long> DecryptAsync(
        Stream encrypted,
        Stream plaintext,
        ReadOnlyMemory<byte> key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        ArgumentNullException.ThrowIfNull(plaintext);
        ValidateKey(key.Span);

        var header = new byte[Magic.Length + NoncePrefixSize];
        await ReadExactlyAsync(encrypted, header, cancellationToken);
        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Unsupported database backup archive format.");

        var input = new byte[ChunkSize];
        var output = new byte[ChunkSize];
        var tag = new byte[TagSize];
        var lengthBytes = new byte[sizeof(int)];
        uint counter = 0;
        long totalPlaintextBytes = 0;
        using var aes = new AesGcm(key.Span, TagSize);

        while (true)
        {
            await ReadExactlyAsync(encrypted, lengthBytes, cancellationToken);
            var count = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
            if (count == 0)
            {
                await ReadExactlyAsync(encrypted, tag, cancellationToken);
                var nonce = CreateNonce(header, counter);
                var terminalAad = CreateTerminalAssociatedData(header, counter, totalPlaintextBytes);
                aes.Decrypt(nonce, ReadOnlySpan<byte>.Empty, tag, Span<byte>.Empty, terminalAad);
                if (totalPlaintextBytes == 0 || await HasRemainingByteAsync(encrypted, cancellationToken))
                    throw new InvalidDataException("Database backup archive has an invalid terminal frame.");
                return totalPlaintextBytes;
            }
            if (count is < 1 or > ChunkSize)
                throw new InvalidDataException("Database backup archive contains an invalid chunk length.");

            await ReadExactlyAsync(encrypted, input.AsMemory(0, count), cancellationToken);
            await ReadExactlyAsync(encrypted, tag, cancellationToken);
            var chunkNonce = CreateNonce(header, counter);
            var associatedData = CreateAssociatedData(header, counter, count);
            aes.Decrypt(chunkNonce, input.AsSpan(0, count), tag, output.AsSpan(0, count), associatedData);
            await plaintext.WriteAsync(output.AsMemory(0, count), cancellationToken);
            totalPlaintextBytes = checked(totalPlaintextBytes + count);
            counter = checked(counter + 1);
        }
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
            throw new ArgumentException("Database backup encryption requires a 256-bit key.", nameof(key));
    }

    private static byte[] CreateNonce(byte[] header, uint counter)
    {
        var nonce = new byte[NonceSize];
        Buffer.BlockCopy(header, MagicSize, nonce, 0, NoncePrefixSize);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixSize), counter);
        return nonce;
    }

    private static byte[] CreateAssociatedData(byte[] header, uint counter, int length)
    {
        var aad = new byte[header.Length + sizeof(uint) + sizeof(int)];
        Buffer.BlockCopy(header, 0, aad, 0, header.Length);
        BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(header.Length), counter);
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(header.Length + sizeof(uint)), length);
        return aad;
    }

    private static byte[] CreateTerminalAssociatedData(byte[] header, uint counter, long totalBytes)
    {
        var aad = new byte[header.Length + sizeof(uint) + sizeof(int) + sizeof(long) + EndMarker.Length];
        Buffer.BlockCopy(header, 0, aad, 0, header.Length);
        var offset = header.Length;
        BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(offset), counter);
        offset += sizeof(uint);
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(offset), 0);
        offset += sizeof(int);
        BinaryPrimitives.WriteInt64BigEndian(aad.AsSpan(offset), totalBytes);
        offset += sizeof(long);
        Buffer.BlockCopy(EndMarker, 0, aad, offset, EndMarker.Length);
        return aad;
    }

    private static byte[] Int32Bytes(int value)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return bytes;
    }

    private static async Task ReadExactlyAsync(
        Stream source,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await source.ReadAsync(buffer[read..], cancellationToken);
            if (count == 0)
                throw new InvalidDataException("Database backup archive is truncated.");
            read += count;
        }
    }

    private static async Task<bool> HasRemainingByteAsync(Stream source, CancellationToken cancellationToken)
    {
        var probe = new byte[1];
        return await source.ReadAsync(probe.AsMemory(), cancellationToken) != 0;
    }
}
