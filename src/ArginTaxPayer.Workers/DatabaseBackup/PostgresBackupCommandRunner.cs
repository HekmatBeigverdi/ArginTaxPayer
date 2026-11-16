using System.Diagnostics;
using Npgsql;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed class PostgresBackupCommandRunner(
    BackupRunnerOptions options,
    string pgDumpPath,
    string pgRestorePath,
    string protectedTemporaryDirectory) : IPostgresBackupCommandRunner
{
    public async Task<long> CreateEncryptedDumpAsync(
        Stream encryptedOutput,
        BackupEncryptionKey backupKey,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(protectedTemporaryDirectory);
        var connection = ParseConnectionString();
        await using var passwordFile = await TemporaryPasswordFile.CreateAsync(
            protectedTemporaryDirectory, connection, cancellationToken);
        var start = NewProcess(pgDumpPath);
        foreach (var argument in new[]
        {
            "--no-password", "--format=custom", "--no-owner", "--no-acl",
            "--host", connection.Host, "--port", connection.Port.ToString(),
            "--username", connection.Username, "--dbname", connection.Database
        })
            start.ArgumentList.Add(argument);
        ConfigurePostgresEnvironment(start, connection, passwordFile.Path);

        using var process = Start(start, "pg_dump");
        process.StandardInput.Close();
        var stderrDrain = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        try
        {
            var encryptedBytes = await BackupEncryption.EncryptAsync(
                process.StandardOutput.BaseStream, encryptedOutput, backupKey.Material, cancellationToken);
            await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stderrDrain);
            if (process.ExitCode != 0)
                throw new BackupCommandFailedException("PostgreSQL dump command failed.");
            return encryptedBytes;
        }
        catch
        {
            await KillAndWaitAsync(process);
            throw;
        }
    }

    public async Task ValidateArchiveAsync(
        Stream encryptedArchive,
        BackupEncryptionKey backupKey,
        CancellationToken cancellationToken)
    {
        var start = NewProcess(pgRestorePath);
        // Generate SQL to drained stdout without connecting to a database. Listing only
        // reads the TOC and exits early, breaking the decryptor's input pipe for large dumps.
        start.ArgumentList.Add("--file=-");
        // No input filename: pg_restore reads the complete archive from stdin.
        using var process = Start(start, "pg_restore");
        var stdoutDrain = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        var stderrDrain = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        try
        {
            await BackupEncryption.DecryptAsync(
                encryptedArchive, process.StandardInput.BaseStream, backupKey.Material, cancellationToken);
            process.StandardInput.Close();
            await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutDrain, stderrDrain);
            if (process.ExitCode != 0)
                throw new BackupCommandFailedException("PostgreSQL archive validation failed.");
        }
        catch
        {
            process.StandardInput.Close();
            await KillAndWaitAsync(process);
            throw;
        }
    }

    private NpgsqlConnectionStringBuilder ParseConnectionString()
    {
        var connection = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        if (string.IsNullOrWhiteSpace(connection.Host) || connection.Host.Contains(',') ||
            string.IsNullOrWhiteSpace(connection.Database) || string.IsNullOrWhiteSpace(connection.Username) ||
            string.IsNullOrWhiteSpace(connection.Password))
            throw new InvalidOperationException("Backup PostgreSQL connection configuration is incomplete.");
        return connection;
    }

    private static ProcessStartInfo NewProcess(string executable) => new(executable)
    {
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    private static Process Start(ProcessStartInfo start, string operation)
    {
        try
        {
            return Process.Start(start) ?? throw new BackupCommandFailedException(
                $"Unable to start {operation}.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new BackupCommandFailedException($"Unable to start {operation}.");
        }
    }

    private static void ConfigurePostgresEnvironment(
        ProcessStartInfo start,
        NpgsqlConnectionStringBuilder connection,
        string passwordFile)
    {
        start.Environment["PGPASSFILE"] = passwordFile;
        start.Environment["PGSSLMODE"] = connection.SslMode switch
        {
            SslMode.VerifyFull => "verify-full",
            SslMode.VerifyCA => "verify-ca",
            SslMode.Require => "require",
            SslMode.Allow => "allow",
            SslMode.Prefer => "prefer",
            _ => "disable"
        };
        if (!string.IsNullOrWhiteSpace(connection.RootCertificate))
            start.Environment["PGSSLROOTCERT"] = connection.RootCertificate;
    }

    private static async Task KillAndWaitAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private sealed class TemporaryPasswordFile : IAsyncDisposable
    {
        private TemporaryPasswordFile(string path) => Path = path;
        public string Path { get; }

        public static async Task<TemporaryPasswordFile> CreateAsync(
            string directory, NpgsqlConnectionStringBuilder connection, CancellationToken cancellationToken)
        {
            var path = System.IO.Path.Combine(directory, "pgpass-" + Guid.NewGuid().ToString("N"));
            var contents = string.Join(':', Escape(connection.Host), connection.Port.ToString(),
                Escape(connection.Database), Escape(connection.Username), Escape(connection.Password));
            await File.WriteAllTextAsync(path, contents + Environment.NewLine, cancellationToken);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return new TemporaryPasswordFile(path);
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return ValueTask.CompletedTask;
        }

        private static string Escape(string value) => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal);
    }
}
