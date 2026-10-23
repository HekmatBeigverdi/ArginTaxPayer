using System.IO.Compression;
using System.Text;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

internal static class SigningKeyDownloadPackage
{
    public static byte[] Create(GeneratedKeyHandoff handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "public.key", handoff.PublicPem);
            Write(archive, "private.key", handoff.PrivatePem);
        }

        return output.ToArray();
    }

    private static void Write(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: false);
        writer.Write(contents);
    }
}
