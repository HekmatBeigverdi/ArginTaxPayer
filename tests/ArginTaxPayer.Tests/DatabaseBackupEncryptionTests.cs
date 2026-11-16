using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class DatabaseBackupEncryptionTests
{
    [Fact]
    public async Task Encryption_round_trip_streams_large_archives_without_changing_bytes()
    {
        var plaintext = RandomNumberGenerator.GetBytes(2 * BackupEncryption.ChunkSize + 37);
        var key = RandomNumberGenerator.GetBytes(32);
        await using var encrypted = new MemoryStream();
        await using var decrypted = new MemoryStream();

        await BackupEncryption.EncryptAsync(
            new MemoryStream(plaintext), encrypted, key, CancellationToken.None);
        encrypted.Position = 0;
        await BackupEncryption.DecryptAsync(encrypted, decrypted, key, CancellationToken.None);

        Assert.Equal(plaintext, decrypted.ToArray());
        Assert.NotEqual(plaintext, encrypted.ToArray());
    }

    [Fact]
    public async Task Decryption_rejects_modified_ciphertext()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        await using var encrypted = new MemoryStream();
        await BackupEncryption.EncryptAsync(
            new MemoryStream("sensitive archive"u8.ToArray()), encrypted, key, CancellationToken.None);
        var bytes = encrypted.ToArray();
        bytes[BackupEncryption.HeaderSize + sizeof(int) + 3] ^= 0x40;
        await using var corrupted = new MemoryStream(bytes);

        await Assert.ThrowsAnyAsync<CryptographicException>(() => BackupEncryption.DecryptAsync(
            corrupted, Stream.Null, key, CancellationToken.None));
    }


    [Fact]
    public async Task Decryption_rejects_truncated_archive_and_missing_final_marker()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        await using var encrypted = new MemoryStream();
        await BackupEncryption.EncryptAsync(
            new MemoryStream("archive"u8.ToArray()), encrypted, key, CancellationToken.None);
        var bytes = encrypted.ToArray()[..^4];

        await Assert.ThrowsAsync<InvalidDataException>(() => BackupEncryption.DecryptAsync(
            new MemoryStream(bytes), Stream.Null, key, CancellationToken.None));
    }

    [Fact]
    public async Task Each_archive_gets_an_independent_random_nonce_prefix()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        await using var first = new MemoryStream();
        await using var second = new MemoryStream();
        var source = "same archive"u8.ToArray();

        await BackupEncryption.EncryptAsync(new MemoryStream(source), first, key, CancellationToken.None);
        await BackupEncryption.EncryptAsync(new MemoryStream(source), second, key, CancellationToken.None);

        Assert.NotEqual(first.ToArray()[..BackupEncryption.HeaderSize],
            second.ToArray()[..BackupEncryption.HeaderSize]);
    }
}

public sealed class DatabaseBackupStoreTests
{
    [Fact]
    public async Task Local_store_publishes_atomically_and_marks_checksum_tampering()
    {
        var root = Path.Combine(Path.GetTempPath(), "argin-backup-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalBackupStore(root);
            var manifest = new BackupManifest(Guid.NewGuid(), DateTime.UtcNow,
                "argintaxpayer_test", "migration-1", "1", true);
            var artifact = await store.PublishAsync(
                new MemoryStream("encrypted bytes"u8.ToArray()), manifest, CancellationToken.None);

            Assert.True(File.Exists(artifact.FilePath));
            Assert.True(File.Exists(artifact.ManifestPath));
            Assert.True(artifact.ChecksumMatches);

            await File.WriteAllTextAsync(artifact.FilePath, "tampered");
            var listed = await store.ListAsync(CancellationToken.None);

            Assert.Single(listed);
            Assert.False(listed[0].ChecksumMatches);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Rolling_retention_removes_only_expired_verified_artifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "argin-backup-retention-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalBackupStore(root);
            var now = DateTime.UtcNow;
            async Task<Guid> Publish(DateTime createdAt)
            {
                var artifact = await store.PublishAsync(
                    new MemoryStream("archive bytes"u8.ToArray()),
                    new BackupManifest(Guid.NewGuid(), createdAt, "argintaxpayer_test", "m1", "1", true),
                    CancellationToken.None);
                return artifact.ArtifactId;
            }

            var expiredId = await Publish(now.AddDays(-8));
            var retainedId = await Publish(now.AddDays(-1));
            var removed = await store.ApplyRetentionAsync(now, TimeSpan.FromDays(7), CancellationToken.None);
            var listed = await store.ListAsync(CancellationToken.None);

            Assert.Contains(expiredId, removed);
            Assert.DoesNotContain(expiredId, listed.Select(x => x.ArtifactId));
            Assert.Contains(listed, x => x.ArtifactId == retainedId && x.ChecksumMatches);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Local_store_lists_malformed_checksum_as_unverified_instead_of_failing()
    {
        var root = Path.Combine(Path.GetTempPath(), "argin-backup-manifest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalBackupStore(root);
            var artifact = await store.PublishAsync(new MemoryStream("encrypted"u8.ToArray()),
                new BackupManifest(Guid.NewGuid(), DateTime.UtcNow, "argintaxpayer_test", "m1", "1", true),
                CancellationToken.None);
            var json = JsonNode.Parse(await File.ReadAllTextAsync(artifact.ManifestPath))!;
            json["sha256"] = "invalid-checksum";
            await File.WriteAllTextAsync(artifact.ManifestPath, json.ToJsonString());

            var listed = await store.ListAsync(CancellationToken.None);

            Assert.Single(listed);
            Assert.False(listed[0].ChecksumMatches);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

public sealed class PostgresBackupRunnerTests
{
    [Fact]
    public async Task Runner_publishes_only_encrypted_validated_database_archive()
    {
        var root = Path.Combine(Path.GetTempPath(), "argin-postgres-runner-" + Guid.NewGuid().ToString("N"));
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = RandomNumberGenerator.GetBytes(8192);
        try
        {
            var store = new LocalBackupStore(root);
            var commands = new FakeBackupCommandRunner(plaintext, key);
            var runner = new PostgresBackupRunner(store,
                new BackupRunnerOptions("Host=localhost;Database=argintaxpayer_test;Username=test;Password=test", "m1"),
                new FixedDatabaseSizeReader(1024), new FixedStorageCapacity(1024L * 1024 * 1024),
                new FixedBackupKeyProvider(key), commands);

            var artifact = await runner.CreateAsync(CancellationToken.None);

            Assert.True(artifact.ChecksumMatches);
            Assert.True(artifact.ArchiveValidated);
            Assert.Equal(1, commands.ValidationCount);
            var encrypted = await File.ReadAllBytesAsync(artifact.FilePath);
            Assert.NotEqual(plaintext, encrypted);
            await using var clear = new MemoryStream();
            await BackupEncryption.DecryptAsync(new MemoryStream(encrypted), clear, key, CancellationToken.None);
            Assert.Equal(plaintext, clear.ToArray());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task Runner_refuses_to_start_dump_when_free_space_is_below_database_size_and_reserve()
    {
        var root = Path.Combine(Path.GetTempPath(), "argin-postgres-space-" + Guid.NewGuid().ToString("N"));
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var commands = new FakeBackupCommandRunner("db"u8.ToArray(), key);
            var runner = new PostgresBackupRunner(new LocalBackupStore(root),
                new BackupRunnerOptions("Host=localhost;Database=argintaxpayer_test;Username=test;Password=test", "m1"),
                new FixedDatabaseSizeReader(1024), new FixedStorageCapacity(1),
                new FixedBackupKeyProvider(key), commands);

            await Assert.ThrowsAsync<InsufficientBackupStorageException>(
                () => runner.CreateAsync(CancellationToken.None));
            Assert.Equal(0, commands.DumpCount);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task Runner_does_not_publish_empty_or_failed_dump()
    {
        var root = Path.Combine(Path.GetTempPath(), "argin-postgres-failure-" + Guid.NewGuid().ToString("N"));
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var commands = new FakeBackupCommandRunner([], key) { FailDump = true };
            var runner = new PostgresBackupRunner(new LocalBackupStore(root),
                new BackupRunnerOptions("Host=localhost;Database=argintaxpayer_test;Username=test;Password=test", "m1"),
                new FixedDatabaseSizeReader(10), new FixedStorageCapacity(1024L * 1024 * 1024),
                new FixedBackupKeyProvider(key), commands);

            await Assert.ThrowsAsync<BackupCommandFailedException>(
                () => runner.CreateAsync(CancellationToken.None));
            Assert.Empty(await new LocalBackupStore(root).ListAsync(CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private sealed class FixedDatabaseSizeReader(long bytes) : IBackupDatabaseSizeReader
    {
        public Task<long> GetDatabaseSizeBytesAsync(CancellationToken cancellationToken) => Task.FromResult(bytes);
    }

    private sealed class FixedStorageCapacity(long bytes) : IBackupStorageCapacity
    {
        public long GetAvailableBytes(string path) => bytes;
    }

    private sealed class FixedBackupKeyProvider(byte[] key) : IBackupKeyProvider
    {
        public Task<BackupEncryptionKey> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new BackupEncryptionKey("1", key.ToArray()));

        public Task<BackupEncryptionKey> GetByVersionAsync(string version, CancellationToken cancellationToken) =>
            Task.FromResult(new BackupEncryptionKey(version, key.ToArray()));
    }

    private sealed class FakeBackupCommandRunner(byte[] plaintext, byte[] key) : IPostgresBackupCommandRunner
    {
        public int DumpCount { get; private set; }
        public int ValidationCount { get; private set; }
        public bool FailDump { get; init; }

        public async Task<long> CreateEncryptedDumpAsync(Stream output, BackupEncryptionKey backupKey,
            CancellationToken cancellationToken)
        {
            DumpCount++;
            if (FailDump) throw new BackupCommandFailedException("PostgreSQL dump failed.");
            await using var source = new MemoryStream(plaintext);
            return await BackupEncryption.EncryptAsync(source, output, key, cancellationToken);
        }

        public async Task ValidateArchiveAsync(Stream encryptedArchive, BackupEncryptionKey backupKey,
            CancellationToken cancellationToken)
        {
            ValidationCount++;
            await BackupEncryption.DecryptAsync(encryptedArchive, Stream.Null, key, cancellationToken);
        }
    }
}

public sealed class PostgresBackupCommandRunnerTests
{
    [Fact]
    public async Task Commands_use_private_password_file_and_validate_archive_via_pipe()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
        var root = Path.Combine(Path.GetTempPath(), "argin-pg-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var argsLog = Path.Combine(root, "dump-args.txt");
            var modeLog = Path.Combine(root, "pass-mode.txt");
            var tlsLog = Path.Combine(root, "tls-mode.txt");
            var pgDump = Path.Combine(root, "pg_dump");
            var pgRestore = Path.Combine(root, "pg_restore");
            await WriteExecutable(pgDump, $"""
                #!/bin/sh
                printf '%s\n' "$@" > '{argsLog}'
                test -f "$PGPASSFILE" || exit 70
                if stat -f %Lp "$PGPASSFILE" > '{modeLog}' 2>/dev/null; then :; else stat -c %a "$PGPASSFILE" > '{modeLog}'; fi
                printf '%s' "$PGSSLMODE" > '{tlsLog}'
                printf 'test archive payload'
                """);
            await WriteExecutable(pgRestore, """
                #!/bin/sh
                test "$1" = "--file=-" || exit 71
                test "$#" = "1" || exit 72
                payload=$(cat)
                test "$payload" = "test archive payload" || exit 73
                """);

            var connection = "Host=localhost;Port=5432;Database=backup_test;Username=tester;Password=local-only-secret;SSL Mode=VerifyFull;Root Certificate=/tmp/test-ca.pem";
            var runner = new PostgresBackupCommandRunner(
                new BackupRunnerOptions(connection, "m1"), pgDump, pgRestore, root);
            var key = new BackupEncryptionKey("1", RandomNumberGenerator.GetBytes(32));
            await using var encrypted = new MemoryStream();

            await runner.CreateEncryptedDumpAsync(encrypted, key, CancellationToken.None);
            encrypted.Position = 0;
            await runner.ValidateArchiveAsync(encrypted, key, CancellationToken.None);

            Assert.DoesNotContain("local-only-secret", await File.ReadAllTextAsync(argsLog));
            Assert.Equal("verify-full", await File.ReadAllTextAsync(tlsLog));
            Assert.Equal("600", (await File.ReadAllTextAsync(modeLog)).Trim());
            Assert.Empty(Directory.EnumerateFiles(root, "pgpass-*"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task WriteExecutable(string path, string body)
    {
        await File.WriteAllTextAsync(path, body);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
