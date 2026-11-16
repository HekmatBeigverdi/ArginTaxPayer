using Npgsql;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record BackupRunnerOptions(string ConnectionString, string SchemaIdentity);

public sealed record BackupEncryptionKey(string Version, byte[] Material);

public interface IBackupKeyProvider
{
    Task<BackupEncryptionKey> GetCurrentAsync(CancellationToken cancellationToken);
    Task<BackupEncryptionKey> GetByVersionAsync(string version, CancellationToken cancellationToken);
}

public interface IBackupDatabaseSizeReader
{
    Task<long> GetDatabaseSizeBytesAsync(CancellationToken cancellationToken);
}

public interface IBackupStorageCapacity
{
    long GetAvailableBytes(string path);
}

public interface IPostgresBackupCommandRunner
{
    Task<long> CreateEncryptedDumpAsync(Stream encryptedOutput, BackupEncryptionKey backupKey,
        CancellationToken cancellationToken);

    Task ValidateArchiveAsync(Stream encryptedArchive, BackupEncryptionKey backupKey,
        CancellationToken cancellationToken);
}

public sealed class InsufficientBackupStorageException()
    : IOException("There is not enough free space to create a verified database backup.");

public sealed class BackupCommandFailedException(string message)
    : InvalidOperationException(message);

public sealed class NpgsqlBackupDatabaseSizeReader(string connectionString) : IBackupDatabaseSizeReader
{
    public async Task<long> GetDatabaseSizeBytesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT pg_database_size(current_database())", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long bytes && bytes >= 0
            ? bytes
            : throw new InvalidOperationException("PostgreSQL did not return the current database size.");
    }
}

public sealed class DriveBackupStorageCapacity : IBackupStorageCapacity
{
    public long GetAvailableBytes(string path)
    {
        // Query the actual filesystem on Unix, including separately mounted storage.
        var existingPath = Path.GetFullPath(path);
        while (!Directory.Exists(existingPath))
        {
            existingPath = Path.GetDirectoryName(existingPath)
                ?? throw new InvalidOperationException("Backup storage volume could not be determined.");
        }
        return new DriveInfo(existingPath).AvailableFreeSpace;
    }
}
