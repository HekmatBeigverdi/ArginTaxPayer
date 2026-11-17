using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ArginTaxPayer.Workers.DatabaseBackup;

/// <summary>Reads current and escrowed historical versioned keys from owner-only host files.</summary>
public sealed class FileBackupKeyProvider(string keyFilePath) : IBackupKeyProvider
{
    public Task<BackupEncryptionKey> GetCurrentAsync(CancellationToken cancellationToken) =>
        ReadKeyAsync(Path.GetFullPath(keyFilePath), expectedVersion: null, cancellationToken);

    public async Task<BackupEncryptionKey> GetByVersionAsync(string version, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(version ?? "", "^[a-zA-Z0-9_-]{1,32}$"))
            throw new ArgumentException("Invalid backup encryption key version.", nameof(version));
        var current = await ReadKeyAsync(Path.GetFullPath(keyFilePath), expectedVersion: null, cancellationToken);
        if (string.Equals(current.Version, version, StringComparison.Ordinal)) return current;
        CryptographicOperations.ZeroMemory(current.Material);
        var history = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(keyFilePath))!,
            "database-backup-keys", version + ".key");
        return await ReadKeyAsync(history, version, cancellationToken);
    }

    private static async Task<BackupEncryptionKey> ReadKeyAsync(string path, string? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new FileNotFoundException("Database backup encryption key is not provisioned.");
        if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) &&
            File.GetUnixFileMode(path) != (UnixFileMode.UserRead | UnixFileMode.UserWrite))
            throw new UnauthorizedAccessException("Database backup key file permissions must be 0600.");
        var info = new FileInfo(path);
        if (info.Length > 256) throw new InvalidDataException("Database backup key file is too large.");
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        if (lines.Length != 2 || !Regex.IsMatch(lines[0], "^[a-zA-Z0-9_-]{1,32}$") ||
            expectedVersion is not null && !string.Equals(lines[0], expectedVersion, StringComparison.Ordinal))
            throw new InvalidDataException("Database backup key file format or version is invalid.");
        byte[] material;
        try { material = Convert.FromHexString(lines[1]); }
        catch (FormatException ex) { throw new InvalidDataException("Database backup key format is invalid.", ex); }
        if (material.Length != 32)
        {
            CryptographicOperations.ZeroMemory(material);
            throw new InvalidDataException("Database backup key must contain 256 bits.");
        }
        return new BackupEncryptionKey(lines[0], material);
    }
}
