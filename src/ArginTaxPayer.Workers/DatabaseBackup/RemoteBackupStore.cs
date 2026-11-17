using System.Globalization;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record RemoteBackupMetadata(string Name, DateTime ModifiedUtc, long SizeBytes, bool IsFile);

public interface IRemoteBackupTransport
{
    Task UploadAtomicallyAsync(string localPath, string remotePath, CancellationToken cancellationToken);
    Task<IReadOnlyList<RemoteBackupMetadata>> ListAsync(string directory, CancellationToken cancellationToken);
    Task DeleteAsync(string remotePath, CancellationToken cancellationToken);
    Task DownloadAsync(string remotePath, string localPath, CancellationToken cancellationToken);
}

public sealed class RemoteBackupStore(IRemoteBackupTransport transport, string remoteDirectory)
{
    private readonly string directory = NormalizeDirectory(remoteDirectory);

    public async Task PublishAsync(BackupArtifactMetadata artifact, CancellationToken cancellationToken)
    {
        if (!artifact.ArchiveValidated || !artifact.ChecksumMatches || artifact.ArtifactId == Guid.Empty ||
            artifact.CreatedAtUtc.Kind != DateTimeKind.Utc || !File.Exists(artifact.FilePath) ||
            !File.Exists(artifact.ManifestPath))
            throw new InvalidDataException("Only locally verified backup artifacts can be transferred.");

        var stamp = artifact.CreatedAtUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
        var baseName = $"{stamp}_{artifact.ArtifactId:N}";
        await transport.UploadAtomicallyAsync(artifact.FilePath, Join(baseName + ".argonbackup"), cancellationToken);
        try
        {
            await transport.UploadAtomicallyAsync(artifact.ManifestPath, Join(baseName + ".manifest.json"), cancellationToken);
        }
        catch
        {
            await transport.DeleteAsync(Join(baseName + ".argonbackup"), CancellationToken.None);
            throw;
        }
    }

    public async Task DownloadAsync(string remoteName, string localPath, CancellationToken cancellationToken)
    {
        if (Path.GetFileName(remoteName) != remoteName || remoteName.Contains('\r') || remoteName.Contains('\n'))
            throw new ArgumentException("Invalid remote backup name.", nameof(remoteName));
        await transport.DownloadAsync(Join(remoteName), localPath, cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteBackupMetadata>> ListAsync(CancellationToken cancellationToken)
    {
        var entries = await transport.ListAsync(directory, cancellationToken);
        var files = entries.Where(x => x.IsFile).ToArray();
        var names = files.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        return files.Where(x => x.Name.EndsWith(".argonbackup", StringComparison.Ordinal) &&
                names.Contains(Path.ChangeExtension(x.Name, null) + ".manifest.json"))
            .OrderByDescending(x => x.ModifiedUtc).ToArray();
    }

    public async Task<IReadOnlyList<string>> ApplyRetentionAsync(
        DateTime nowUtc, TimeSpan rollingRetention, int monthlyRetentionCount,
        CancellationToken cancellationToken)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Retention clock must be UTC.", nameof(nowUtc));
        if (rollingRetention <= TimeSpan.Zero || rollingRetention > TimeSpan.FromDays(7))
            throw new ArgumentOutOfRangeException(nameof(rollingRetention));
        if (monthlyRetentionCount is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(monthlyRetentionCount));

        var entries = await transport.ListAsync(directory, cancellationToken);
        var files = entries.Where(x => x.IsFile).ToArray();
        var names = files.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var orphan in files.Where(x => x.ModifiedUtc < nowUtc - rollingRetention &&
                     (x.Name.Contains(".partial-", StringComparison.Ordinal) ||
                      x.Name.EndsWith(".argonbackup", StringComparison.Ordinal) &&
                      !names.Contains(Path.ChangeExtension(x.Name, null) + ".manifest.json"))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await transport.DeleteAsync(Join(orphan.Name), cancellationToken);
            if (orphan.Name.EndsWith(".argonbackup", StringComparison.Ordinal))
                await transport.DeleteAsync(Join(Path.ChangeExtension(orphan.Name, null) + ".manifest.json"), cancellationToken);
        }
        var artifacts = await ListAsync(cancellationToken);
        var keep = artifacts.Where(x => x.ModifiedUtc >= nowUtc - rollingRetention).Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var monthGroup in artifacts.Where(x => x.ModifiedUtc < nowUtc - rollingRetention)
                     .GroupBy(x => (x.ModifiedUtc.Year, x.ModifiedUtc.Month))
                     .OrderByDescending(x => x.Key.Year).ThenByDescending(x => x.Key.Month)
                     .Take(monthlyRetentionCount))
        {
            var point = monthGroup.OrderByDescending(x => x.ModifiedUtc).First();
            keep.Add(point.Name);
        }

        var removed = new List<string>();
        foreach (var item in artifacts.Where(x => !keep.Contains(x.Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await transport.DeleteAsync(Join(item.Name), cancellationToken);
            var manifest = Path.ChangeExtension(item.Name, null) + ".manifest.json";
            await transport.DeleteAsync(Join(manifest), cancellationToken);
            removed.Add(item.Name);
        }
        return removed;
    }

    private string Join(string name) => directory == "/" ? "/" + name : directory + "/" + name;

    private static string NormalizeDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\r') || value.Contains('\n') || value.Contains('\\'))
            throw new ArgumentException("Invalid remote backup directory.", nameof(value));
        var path = "/" + string.Join('/', value.Split('/', StringSplitOptions.RemoveEmptyEntries));
        if (path.Split('/').Any(part => part is "." or ".."))
            throw new ArgumentException("Invalid remote backup directory.", nameof(value));
        return path;
    }
}
