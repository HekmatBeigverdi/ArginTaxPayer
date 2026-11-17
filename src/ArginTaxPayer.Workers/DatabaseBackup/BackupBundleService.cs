using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Domain.Administration;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed class BackupBundleException(string message) : IOException(message);

/// <summary>Private, bounded transport of encrypted archives. Keys never enter bundles.</summary>
public sealed class BackupBundleService(IConfiguration configuration)
{
    private readonly SemaphoreSlim storageGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public long MaximumBytes => Math.Clamp(configuration.GetValue<long?>("BackupController:MaximumBundleBytes") ?? 2L * 1024 * 1024 * 1024, 1024, 16L * 1024 * 1024 * 1024);
    private string TempRoot => Path.Combine(configuration["BackupController:StateDirectory"] ?? "/var/lib/argintaxpayer/backup-controller", "bundle-tmp");

    private string NewTemporaryPath()
    {
        Directory.CreateDirectory(TempRoot);
        if ((File.GetAttributes(TempRoot) & FileAttributes.ReparsePoint) != 0)
            throw new BackupBundleException("backup_bundle_invalid_temp_directory");
        File.SetUnixFileMode(TempRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return Path.Combine(TempRoot, Guid.NewGuid().ToString("N") + ".tmp");
    }
    // Called once before the web host/work queue starts; no transfer is active yet.
    public void CleanupAbandonedFiles()
    {
        if (!Directory.Exists(TempRoot)) return;
        if ((File.GetAttributes(TempRoot) & FileAttributes.ReparsePoint) != 0)
            throw new BackupBundleException("backup_bundle_invalid_temp_directory");
        foreach (var file in Directory.EnumerateFiles(TempRoot, "*.tmp", SearchOption.TopDirectoryOnly)) File.Delete(file);
    }
    public sealed class DownloadStream(string path, SemaphoreSlim gate, string fileName)
        : FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose)
    {
        public string FileName { get; } = fileName;
        private int released;
        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally { if (disposing && Interlocked.Exchange(ref released, 1) == 0) gate.Release(); }
        }
        public override async ValueTask DisposeAsync()
        {
            try { await base.DisposeAsync(); }
            finally { if (Interlocked.Exchange(ref released, 1) == 0) gate.Release(); }
        }
    }
    private static FileStream CreatePrivate(string path) => new(path, new FileStreamOptions
    {
        Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None,
        Options = FileOptions.Asynchronous, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
    });
    public async Task<string> ReceiveAsync(Stream body, CancellationToken ct)
    {
        if (!await storageGate.WaitAsync(0, ct)) throw new BackupBundleException("backup_bundle_transfer_busy");
        string? path = null;
        try
        {
            path = NewTemporaryPath();
            await using var output = CreatePrivate(path);
            var usable = new DriveBackupStorageCapacity().GetAvailableBytes(TempRoot) - 512L * 1024 * 1024;
            if (usable < 1024) throw new BackupBundleException("backup_bundle_insufficient_space");
            await CopyBoundedAsync(body, output, Math.Min(MaximumBytes, usable), ct);
            return path;
        }
        catch { if (path is not null) File.Delete(path); throw; }
        finally { storageGate.Release(); }
    }
    private static async Task<long> CopyBoundedAsync(Stream source, Stream destination, long limit, CancellationToken ct)
    {
        var buffer = new byte[128 * 1024]; long total = 0; int read;
        while ((read = await source.ReadAsync(buffer, ct)) != 0)
        {
            total = checked(total + read);
            if (total > limit) throw new BackupBundleException("backup_bundle_too_large");
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (total == 0) throw new BackupBundleException("backup_bundle_empty");
        return total;
    }
    public async Task<DownloadStream> DownloadAsync(DatabaseBackupSettings settings, Guid id, CancellationToken ct)
    {
        var artifact = (await new LocalBackupStore(settings.LocalPath).ListAsync(ct)).SingleOrDefault(x => x.ArtifactId == id);
        if (artifact is null || !artifact.ArchiveValidated || !artifact.ChecksumMatches)
            throw new BackupBundleException("backup_artifact_not_verified");
        if (!await storageGate.WaitAsync(0, ct)) throw new BackupBundleException("backup_bundle_transfer_busy");
        string? path = null;
        try
        {
            path = NewTemporaryPath();
            if (new DriveBackupStorageCapacity().GetAvailableBytes(TempRoot) < artifact.EncryptedBytes + 1024L * 1024 + 512L * 1024 * 1024)
                throw new BackupBundleException("backup_bundle_insufficient_space");
            await using (var output = CreatePrivate(path))
            {
                using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var file in new[] { artifact.FilePath, artifact.ManifestPath })
                {
                    var entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.NoCompression);
                    await using var target = entry.Open();
                    await using var source = File.OpenRead(file);
                    await source.CopyToAsync(target, ct);
                }
            }
            return new DownloadStream(path, storageGate, BackupDatabaseIdentity.DownloadName(artifact.DatabaseName, id, configuration));
        }
        catch { if (path is not null) File.Delete(path); storageGate.Release(); throw; }
    }
    public async Task ImportAsync(string bundlePath, DatabaseBackupSettings settings,
        BackupOperationStore operations, Guid operationId, CancellationToken ct)
    {
        var plan = BackupProgressPlan.ForImport();
        await storageGate.WaitAsync(ct);
        string? extracted = null;
        try
        {
            extracted = NewTemporaryPath();
            await plan.ReportStageAsync(operations, operationId, "validate-bundle", ct);
            using var zip = ZipFile.OpenRead(bundlePath);
            if (zip.Entries.Count != 2) throw new BackupBundleException("backup_bundle_requires_archive_and_manifest");
            var manifestEntry = zip.Entries.SingleOrDefault(x => x.FullName.EndsWith(".manifest.json", StringComparison.Ordinal));
            if (manifestEntry is null || manifestEntry.Length is < 1 or > 16384)
                throw new BackupBundleException("backup_bundle_invalid_manifest");
            BackupManifest? manifest;
            await using (var input = manifestEntry.Open())
            {
                using var content = new MemoryStream();
                await CopyBoundedAsync(input, content, 16384, ct); content.Position = 0;
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(content, JsonOptions, ct);
            }
            if (manifest is null || manifest.ArtifactId == Guid.Empty || manifest.CreatedAtUtc.Kind != DateTimeKind.Utc ||
                manifest.EncryptedBytes < 1 || manifest.EncryptedBytes > MaximumBytes ||
                string.IsNullOrWhiteSpace(manifest.DatabaseName) ||
                manifest.SchemaIdentity != (configuration["BackupController:SchemaIdentity"] ?? "phase18"))
                throw new BackupBundleException("backup_bundle_invalid_manifest_or_schema");
            var basename = manifest.ArtifactId.ToString("N");
            var entry = zip.GetEntry(basename + ".argonbackup");
            if (manifestEntry.FullName != basename + ".manifest.json" || entry is null || entry.Length != manifest.EncryptedBytes ||
                zip.Entries.Any(x => x.FullName != x.Name || ((x.ExternalAttributes >> 16) & 0xF000) == 0xA000))
                throw new BackupBundleException("backup_bundle_invalid_entries");
            var store = new LocalBackupStore(settings.LocalPath);
            if (File.Exists(store.GetArtifactPath(manifest.ArtifactId)) || File.Exists(Path.Combine(settings.LocalPath, basename + ".manifest.json")))
                throw new BackupBundleException("backup_artifact_already_exists");
            if (new DriveBackupStorageCapacity().GetAvailableBytes(TempRoot) < checked(manifest.EncryptedBytes * 2 + 512L * 1024 * 1024))
                throw new BackupBundleException("backup_bundle_insufficient_space");
            await using (var output = CreatePrivate(extracted))
            await using (var source = entry.Open())
            {
                if (await CopyBoundedAsync(source, output, manifest.EncryptedBytes, ct) != manifest.EncryptedBytes)
                    throw new BackupBundleException("backup_bundle_size_mismatch");
            }
            await using (var source = File.OpenRead(extracted))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, ct));
                if (!string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new BackupBundleException("backup_bundle_checksum_mismatch");
            }
            await plan.ReportStageAsync(operations, operationId, "verify-upload", ct);
            BackupEncryptionKey key;
            try { key = await new FileBackupKeyProvider(configuration["BackupController:EncryptionKeyPath"] ?? "/etc/argintaxpayer/database-backup.key").GetByVersionAsync(manifest.EncryptionKeyVersion, ct); }
            catch (Exception ex) when (ex is FileNotFoundException or ArgumentException or InvalidDataException)
            { throw new BackupBundleException("backup_bundle_key_unavailable"); }
            try
            {
                await using var archive = File.OpenRead(extracted);
                await new PostgresBackupCommandRunner(new BackupRunnerOptions(configuration.GetConnectionString("ArginTaxPayerDb")!, manifest.SchemaIdentity),
                    configuration["BackupController:PgDumpPath"] ?? "/usr/bin/pg_dump",
                    configuration["BackupController:PgRestorePath"] ?? "/usr/bin/pg_restore", TempRoot).ValidateArchiveAsync(archive, key, ct);
            }
            catch (CryptographicException) { throw new BackupBundleException("backup_bundle_wrong_key_or_corrupt_archive"); }
            finally { CryptographicOperations.ZeroMemory(key.Material); }
            await plan.ReportStageAsync(operations, operationId, "publish-import", ct);
            if (new DriveBackupStorageCapacity().GetAvailableBytes(settings.LocalPath) < manifest.EncryptedBytes + 512L * 1024 * 1024)
                throw new BackupBundleException("backup_bundle_insufficient_space");
            await using (var archive = File.OpenRead(extracted))
                await store.PublishAsync(archive, manifest with { ArchiveValidated = true }, ct);
            await operations.CompleteAsync(operationId, true, "بستهٔ ZIP اعتبارسنجی و ثبت شد؛ برای ادامه، آماده‌سازی بازیابی را انتخاب کنید.", ct);
        }
        catch (BackupBundleException) { throw; }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException)
        { throw new BackupBundleException("backup_bundle_invalid_archive"); }
        finally
        {
            try { if (extracted is not null) File.Delete(extracted); File.Delete(bundlePath); }
            finally { storageGate.Release(); }
        }
    }
}
