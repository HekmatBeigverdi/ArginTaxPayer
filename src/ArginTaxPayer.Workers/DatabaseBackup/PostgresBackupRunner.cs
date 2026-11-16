using System.Security.Cryptography;
using Npgsql;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed class PostgresBackupRunner(
    LocalBackupStore store,
    BackupRunnerOptions options,
    IBackupDatabaseSizeReader databaseSize,
    IBackupStorageCapacity capacity,
    IBackupKeyProvider keyProvider,
    IPostgresBackupCommandRunner commandRunner,
    BackupProgressPlan? progressPlan = null,
    BackupOperationStore? operations = null,
    Guid? operationId = null)
{
    private const long MinimumReserveBytes = 512L * 1024 * 1024;

    public async Task<BackupArtifactMetadata> CreateAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        await ReportStageAsync("prepare-backup", cancellationToken);
        var databaseBytes = await databaseSize.GetDatabaseSizeBytesAsync(cancellationToken);
        if (databaseBytes < 1)
            throw new InvalidOperationException("The configured PostgreSQL database is empty or unavailable.");
        var reserveBytes = Math.Max(MinimumReserveBytes, databaseBytes / 10);
        var requiredBytes = checked(databaseBytes + reserveBytes);
        if (capacity.GetAvailableBytes(store.RootPath) < requiredBytes)
            throw new InsufficientBackupStorageException();

        var key = await keyProvider.GetCurrentAsync(cancellationToken);
        if (key.Material.Length != 32 || string.IsNullOrWhiteSpace(key.Version))
        {
            CryptographicOperations.ZeroMemory(key.Material);
            throw new InvalidOperationException("A valid backup encryption key is unavailable.");
        }

        var id = Guid.NewGuid();
        var partialPath = store.CreatePartialPath(id);
        try
        {
            await ReportStageAsync("dump-encrypt", cancellationToken);
            await using (var output = new FileStream(partialPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 128 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
            }))
            {
                var dumpedBytes = await commandRunner.CreateEncryptedDumpAsync(output, key, cancellationToken);
                if (dumpedBytes < 1 || output.Length < 1)
                    throw new InvalidDataException("PostgreSQL produced an empty backup archive.");
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }

            await ReportStageAsync("verify-archive", cancellationToken);
            await using (var input = new FileStream(partialPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await commandRunner.ValidateArchiveAsync(input, key, cancellationToken);
            }

            await ReportStageAsync("publish-local", cancellationToken);
            var databaseName = new NpgsqlConnectionStringBuilder(options.ConnectionString).Database;
            var manifest = new BackupManifest(id, DateTime.UtcNow, databaseName,
                options.SchemaIdentity, key.Version, ArchiveValidated: true);
            var artifact = await store.PublishPartialAsync(partialPath, manifest, cancellationToken);
            await store.ApplyRetentionAsync(DateTime.UtcNow, TimeSpan.FromDays(7), cancellationToken);
            return artifact;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key.Material);
            TryDelete(partialPath);
        }
    }

    private Task ReportStageAsync(string stage, CancellationToken cancellationToken) =>
        progressPlan is not null && operations is not null && operationId is Guid id
            ? progressPlan.ReportStageAsync(operations, id, stage, cancellationToken)
            : Task.CompletedTask;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
