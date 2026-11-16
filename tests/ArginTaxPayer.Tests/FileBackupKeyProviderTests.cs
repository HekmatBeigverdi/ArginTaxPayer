using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FileBackupKeyProviderTests
{
    [Fact]
    public async Task Key_provider_reads_current_and_historical_version_files()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var current = Path.Combine(root, "database-backup.key");
        var history = Path.Combine(root, "database-backup-keys");
        Directory.CreateDirectory(history);
        try
        {
            await File.WriteAllTextAsync(current, "v2\n" + Convert.ToHexString(new byte[32]));
            await File.WriteAllTextAsync(Path.Combine(history, "v1.key"), "v1\n" + Convert.ToHexString(Enumerable.Repeat((byte)7, 32).ToArray()));
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(current, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.SetUnixFileMode(Path.Combine(history, "v1.key"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            var provider = new FileBackupKeyProvider(current);
            var active = await provider.GetCurrentAsync(CancellationToken.None);
            var old = await provider.GetByVersionAsync("v1", CancellationToken.None);
            Assert.Equal("v2", active.Version);
            Assert.Equal("v1", old.Version);
            Assert.Equal(32, old.Material.Length);
        }
        finally { Directory.Delete(root, true); }
    }
}
