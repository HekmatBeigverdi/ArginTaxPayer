using System.Security.Cryptography;
using System.Text.Json;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed class LocalBackupStore(string rootPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string root = Path.GetFullPath(rootPath);

    public string RootPath => root;

    public async Task<BackupArtifactMetadata> PublishAsync(
        Stream encryptedArchive,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(encryptedArchive);
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.ArtifactId == Guid.Empty || !manifest.ArchiveValidated ||
            manifest.CreatedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidDataException("Only verified UTC backup artifacts can be published.");

        var partialPath = CreatePartialPath(manifest.ArtifactId);
        try
        {
            await using (var output = new FileStream(partialPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 128 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                UnixCreateMode = PrivateFileMode
            }))
            {
                await encryptedArchive.CopyToAsync(output, 128 * 1024, cancellationToken);
                if (output.Length == 0)
                    throw new InvalidDataException("Backup archive is empty.");
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }
            return await PublishPartialAsync(partialPath, manifest, cancellationToken);
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    public string CreatePartialPath(Guid artifactId)
    {
        if (artifactId == Guid.Empty)
            throw new ArgumentException("Artifact ID is required.", nameof(artifactId));
        EnsureRoot();
        return ArtifactPath(artifactId) + ".partial";
    }

    public async Task<BackupArtifactMetadata> PublishPartialAsync(
        string partialPath,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.ArtifactId == Guid.Empty || !manifest.ArchiveValidated ||
            manifest.CreatedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidDataException("Only verified UTC backup artifacts can be published.");
        EnsureRoot();
        var expectedPartial = CreatePartialPath(manifest.ArtifactId);
        if (!string.Equals(Path.GetFullPath(partialPath), expectedPartial, StringComparison.Ordinal) ||
            !File.Exists(expectedPartial) ||
            (File.GetAttributes(expectedPartial) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Backup partial file is outside the configured storage directory.");

        var artifactPath = ArtifactPath(manifest.ArtifactId);
        var manifestPath = ManifestPath(manifest.ArtifactId);
        var partialManifestPath = manifestPath + ".partial";
        var publishedArchive = false;
        var publishedManifest = false;
        try
        {
            var fileInfo = new FileInfo(expectedPartial);
            if (fileInfo.Length == 0)
                throw new InvalidDataException("Backup archive is empty.");
            var checksum = await ComputeSha256Async(expectedPartial, cancellationToken);
            var completedManifest = manifest with
            {
                Sha256 = checksum,
                EncryptedBytes = fileInfo.Length
            };
            await using (var manifestStream = new FileStream(partialManifestPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                UnixCreateMode = PrivateFileMode
            }))
            {
                await JsonSerializer.SerializeAsync(manifestStream, completedManifest, JsonOptions, cancellationToken);
                await manifestStream.FlushAsync(cancellationToken);
                manifestStream.Flush(flushToDisk: true);
            }

            File.Move(expectedPartial, artifactPath);
            publishedArchive = true;
            File.Move(partialManifestPath, manifestPath);
            publishedManifest = true;
            return ToMetadata(completedManifest, artifactPath, manifestPath, checksumMatches: true);
        }
        catch
        {
            TryDelete(expectedPartial);
            TryDelete(partialManifestPath);
            if (publishedArchive) TryDelete(artifactPath);
            if (publishedManifest) TryDelete(manifestPath);
            throw;
        }
    }

    public async Task<IReadOnlyList<BackupArtifactMetadata>> ListAsync(
        CancellationToken cancellationToken)
    {
        var result = new List<BackupArtifactMetadata>();
        // Listing is read-only. Do not create/chmod the configured storage root
        // merely to show an empty list, especially when running the UI locally
        // against a server path that the developer cannot access.
        if (!Directory.Exists(root))
            return result;
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Backup storage directory cannot be a reparse point.");
        foreach (var path in Directory.EnumerateFiles(root, "*.argonbackup", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                continue;
            var idText = Path.GetFileNameWithoutExtension(path);
            if (!Guid.TryParseExact(idText, "N", out var id))
                continue;
            var manifestPath = ManifestPath(id);
            if (!File.Exists(manifestPath) || (File.GetAttributes(manifestPath) & FileAttributes.ReparsePoint) != 0)
                continue;

            BackupManifest? manifest;
            try
            {
                await using var input = File.OpenRead(manifestPath);
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(input, JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                continue;
            }
            if (manifest is null || manifest.ArtifactId != id || !manifest.ArchiveValidated)
                continue;
            var info = new FileInfo(path);
            var checksumMatches = false;
            var expectedChecksum = manifest.Sha256;
            if (info.Length == manifest.EncryptedBytes &&
                !string.IsNullOrWhiteSpace(expectedChecksum) && expectedChecksum.Length == 64)
            {
                try
                {
                    checksumMatches = CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(await ComputeSha256Async(path, cancellationToken)),
                        Convert.FromHexString(expectedChecksum));
                }
                catch (FormatException) { }
            }
            result.Add(ToMetadata(manifest, path, manifestPath, checksumMatches));
        }
        return result.OrderByDescending(x => x.CreatedAtUtc).ToArray();
    }

    public async Task<IReadOnlyList<Guid>> ApplyRetentionAsync(
        DateTime nowUtc,
        TimeSpan retention,
        CancellationToken cancellationToken)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Retention clock must be UTC.", nameof(nowUtc));
        if (retention <= TimeSpan.Zero || retention > TimeSpan.FromDays(7))
            throw new ArgumentOutOfRangeException(nameof(retention));

        var cutoff = nowUtc - retention;
        var artifacts = await ListAsync(cancellationToken);
        var removed = new List<Guid>();
        foreach (var artifact in artifacts.Where(x => x.CreatedAtUtc < cutoff))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryDelete(artifact.FilePath);
            TryDelete(artifact.ManifestPath);
            if (!File.Exists(artifact.FilePath) && !File.Exists(artifact.ManifestPath))
                removed.Add(artifact.ArtifactId);
        }
        return removed;
    }

    public string GetArtifactPath(Guid artifactId) => ArtifactPath(artifactId);

    private void EnsureRoot()
    {
        Directory.CreateDirectory(root);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private string ArtifactPath(Guid artifactId) => Path.Combine(root, artifactId.ToString("N") + ".argonbackup");
    private string ManifestPath(Guid artifactId) => Path.Combine(root, artifactId.ToString("N") + ".manifest.json");

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static BackupArtifactMetadata ToMetadata(
        BackupManifest manifest, string path, string manifestPath, bool checksumMatches) =>
        new(manifest.ArtifactId, manifest.CreatedAtUtc, manifest.DatabaseName,
            manifest.SchemaIdentity, manifest.EncryptionKeyVersion, manifest.EncryptedBytes,
            manifest.Sha256, manifest.ArchiveValidated, checksumMatches, path, manifestPath);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static readonly UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;
}
