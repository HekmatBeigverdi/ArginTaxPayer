using System.Diagnostics;
using Npgsql;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public interface IRestoreDatabaseCommands
{
    Task RestoreIsolatedAsync(Stream encryptedArchive, BackupEncryptionKey key, string databaseName,
        CancellationToken cancellationToken);
    Task DropIsolatedAsync(string databaseName, CancellationToken cancellationToken);
}

public sealed class PostgresRestoreDatabaseCommands(
    string connectionString, string applicationRole, string pgRestorePath, string protectedTempDirectory) : IRestoreDatabaseCommands
{
    public async Task RestoreIsolatedAsync(Stream encryptedArchive, BackupEncryptionKey key,
        string databaseName, CancellationToken cancellationToken)
    {
        ValidateDatabaseName(databaseName);
        Directory.CreateDirectory(protectedTempDirectory);
        var admin = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(applicationRole) ||
            !System.Text.RegularExpressions.Regex.IsMatch(applicationRole, "^[a-zA-Z0-9_]+$"))
            throw new InvalidOperationException("Application PostgreSQL role name is not supported for isolated restore.");
        admin.Database = "postgres";
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {Quote(databaseName)} OWNER {Quote(applicationRole)}", connection);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            var target = new NpgsqlConnectionStringBuilder(connectionString) { Database = databaseName };
            await using var passwordFile = await CreatePasswordFileAsync(protectedTempDirectory, target, cancellationToken);
            var start = new ProcessStartInfo(pgRestorePath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "--exit-on-error", "--no-owner", "--no-acl", "--host", target.Host,
                         "--port", target.Port.ToString(), "--username", target.Username, "--dbname", databaseName,
                         "--role", applicationRole, "-" })
                start.ArgumentList.Add(argument);
            start.Environment["PGPASSFILE"] = passwordFile.Path;
            start.Environment["PGSSLMODE"] = target.SslMode switch
            {
                SslMode.VerifyFull => "verify-full", SslMode.VerifyCA => "verify-ca", SslMode.Require => "require",
                SslMode.Allow => "allow", SslMode.Prefer => "prefer", _ => "disable"
            };
            if (!string.IsNullOrWhiteSpace(target.RootCertificate)) start.Environment["PGSSLROOTCERT"] = target.RootCertificate;
            using var process = Process.Start(start) ?? throw new InvalidOperationException("PostgreSQL restore command could not start.");
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
            var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
            try
            {
                await BackupEncryption.DecryptAsync(encryptedArchive, process.StandardInput.BaseStream, key.Material, cancellationToken);
                process.StandardInput.Close();
                await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdout, stderr);
                if (process.ExitCode != 0) throw new InvalidDataException("PostgreSQL could not restore the selected archive.");
            }
            catch
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                throw;
            }
            await ValidateRestoredDatabaseAsync(target.ConnectionString, cancellationToken);
        }
        catch
        {
            await DropIsolatedAsync(databaseName, CancellationToken.None);
            throw;
        }
    }

    public async Task DropIsolatedAsync(string databaseName, CancellationToken cancellationToken)
    {
        ValidateDatabaseName(databaseName);
        var admin = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname=@name AND pid<>pg_backend_pid()", connection);
        terminate.Parameters.AddWithValue("name", databaseName);
        await terminate.ExecuteNonQueryAsync(cancellationToken);
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {Quote(databaseName)}", connection);
        await drop.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ValidateRestoredDatabaseAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema')", connection);
        var count = (long)(await command.ExecuteScalarAsync(ct) ?? 0L);
        if (count < 1) throw new InvalidDataException("The restored database contains no application tables.");
    }

    private static async Task<PrivatePgPassFile> CreatePasswordFileAsync(string directory,
        NpgsqlConnectionStringBuilder connection, CancellationToken ct)
    {
        var path = Path.Combine(directory, "pgpass-restore-" + Guid.NewGuid().ToString("N"));
        var content = string.Join(':', Escape(connection.Host), connection.Port.ToString(),
            Escape(connection.Database), Escape(connection.Username), Escape(connection.Password)) + Environment.NewLine;
        await File.WriteAllTextAsync(path, content, ct);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return new PrivatePgPassFile(path);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private static void ValidateDatabaseName(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^recovery_[a-f0-9]{12}$"))
            throw new ArgumentException("Invalid isolated recovery database name.", nameof(name));
    }
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal);

    private sealed class PrivatePgPassFile(string path) : IAsyncDisposable
    {
        public string Path { get; } = path;
        public ValueTask DisposeAsync()
        {
            try { File.Delete(Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return ValueTask.CompletedTask;
        }
    }
}
