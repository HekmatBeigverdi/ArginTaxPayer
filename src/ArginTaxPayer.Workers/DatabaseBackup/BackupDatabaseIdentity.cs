using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public static class BackupDatabaseIdentity
{
    public static string DisplayName(string database, IConfiguration configuration) =>
        Regex.IsMatch(database, "^recovery_[a-f0-9]{12}$")
            ? configuration["BackupController:SourceDatabaseIdentity"] ?? database
            : database;

    public static string DownloadName(string database, Guid id, IConfiguration configuration)
    {
        var name = Regex.Replace(DisplayName(database, configuration), "[^A-Za-z0-9_-]", "_");
        return $"{name}_{id:N}.zip";
    }
}
