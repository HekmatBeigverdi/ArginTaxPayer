using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public class FtpsCredentialPersistenceTests
{
    [Fact]
    public async Task Credentials_survive_reopening_without_changing_parent_permissions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "argin-ftps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var original = File.GetUnixFileMode(directory);
            var path = Path.Combine(directory, "ftps.json");
            await new FtpsCredentialStore(path).SaveAsync("backup", "private-test-password", CancellationToken.None);
            var saved = await new FtpsCredentialStore(path).ReadAsync(CancellationToken.None);
            Assert.Equal("backup", saved!.Username);
            Assert.Equal("private-test-password", saved.Password);
            Assert.Equal(original, File.GetUnixFileMode(directory));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally { Directory.Delete(directory, true); }
    }
}
