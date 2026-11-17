using ArginTaxPayer.Workers.DatabaseBackup;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public class BackupDatabaseIdentityTests
{
    [Theory]
    [InlineData("recovery_e5170d0dfa91", "argintaxpayer_prod")]
    [InlineData("argintaxpayer_test", "argintaxpayer_test")]
    public void Historical_recovery_titles_and_downloads_use_original_identity(string storedName, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["BackupController:SourceDatabaseIdentity"] = "argintaxpayer_prod" }).Build();
        Assert.Equal(expected, BackupDatabaseIdentity.DisplayName(storedName, configuration));
        Assert.Equal($"{expected}_{Guid.Empty:N}.zip", BackupDatabaseIdentity.DownloadName(storedName, Guid.Empty, configuration));
    }
}
