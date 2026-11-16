using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class DatabaseBackupRemoteStoreTests
{
    [Fact]
    public async Task Publish_uses_atomic_remote_names_and_retention_keeps_recent_and_monthly_points()
    {
        var transport = new FakeRemoteBackupTransport();
        var store = new RemoteBackupStore(transport, "/backups");
        var artifacts = new List<BackupArtifactMetadata>();
        var start = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var month = 0; month < 15; month++)
        {
            var timestamp = start.AddMonths(month);
            var artifact = TempArtifact(timestamp);
            artifacts.Add(artifact);
            await store.PublishAsync(artifact, CancellationToken.None);
        }

        var removed = await store.ApplyRetentionAsync(start.AddMonths(15), TimeSpan.FromDays(7), 12,
            CancellationToken.None);

        Assert.Equal(30, transport.Published.Count);
        Assert.Equal(3, removed.Count);
        Assert.Equal(12, (await store.ListAsync(CancellationToken.None)).Count);
        Assert.All(transport.Published, item => Assert.DoesNotContain(".partial", item));
        foreach (var artifact in artifacts) File.Delete(artifact.FilePath);
    }

    [Fact]
    public async Task Interrupted_publication_does_not_leave_remote_final_artifact()
    {
        var transport = new FakeRemoteBackupTransport { FailNextUpload = true };
        var store = new RemoteBackupStore(transport, "/backups");
        var artifact = TempArtifact(DateTime.UtcNow);

        await Assert.ThrowsAsync<IOException>(() => store.PublishAsync(artifact, CancellationToken.None));

        Assert.Empty(await store.ListAsync(CancellationToken.None));
        File.Delete(artifact.FilePath);
        File.Delete(artifact.ManifestPath);
    }

    private static BackupArtifactMetadata TempArtifact(DateTime time)
    {
        var path = Path.GetTempFileName();
        var manifest = path + ".json";
        File.WriteAllText(path, Guid.NewGuid().ToString("N"));
        File.WriteAllText(manifest, "{}");
        return new(Guid.NewGuid(), DateTime.SpecifyKind(time, DateTimeKind.Utc), "database", "schema", "v1",
            new FileInfo(path).Length, "AABB", true, true, path, manifest);
    }

    private sealed class FakeRemoteBackupTransport : IRemoteBackupTransport
    {
        public List<string> Published { get; } = [];
        public bool FailNextUpload { get; set; }
        private readonly Dictionary<string, RemoteBackupMetadata> items = new();

        public Task UploadAtomicallyAsync(string localPath, string remotePath, CancellationToken cancellationToken)
        {
            if (FailNextUpload)
            {
                FailNextUpload = false;
                throw new IOException("interrupted");
            }
            var name = Path.GetFileName(remotePath);
            var timestampText = name[..19];
            var modified = DateTime.ParseExact(timestampText, "yyyyMMddTHHmmssfffZ",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
            items[remotePath] = new(name, DateTime.SpecifyKind(modified, DateTimeKind.Utc),
                new FileInfo(localPath).Length, true);
            Published.Add(remotePath);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RemoteBackupMetadata>> ListAsync(string directory, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RemoteBackupMetadata>>(items.Values.ToArray());
        public Task DownloadAsync(string remotePath, string localPath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
        {
            items.Remove(remotePath);
            return Task.CompletedTask;
        }
    }
}
