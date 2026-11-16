using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FtpsCredentialStoreTests
{
    [Fact]
    public async Task Secret_is_saved_separately_with_private_permissions_and_never_returned_by_status()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "private", "ftps.json");
        try
        {
            var store = new FtpsCredentialStore(path);
            Assert.False(store.IsConfigured);
            await store.SaveAsync("ftp-user", "top-secret", CancellationToken.None);
            Assert.True(store.IsConfigured);
            var read = await store.ReadAsync(CancellationToken.None);
            Assert.Equal("ftp-user", read!.Username);
            Assert.Equal("top-secret", read.Password);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.DoesNotContain("top-secret", store.IsConfigured.ToString());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
