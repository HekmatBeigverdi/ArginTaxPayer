using System.Diagnostics;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public interface IBackupHostCommandRunner
{
    Task RunAsync(string action, CancellationToken cancellationToken);
}

public interface IBackupDatabaseSwitcher
{
    Task ActivateAsync(string databaseName, CancellationToken cancellationToken);
}

public sealed class BackupHostLifecycleException(string action, int exitCode, string detail)
    : InvalidOperationException($"Backup host action {action} failed (exit {exitCode}): {detail}");

public sealed class ProcessBackupHostCommandRunner(
    string helperPath = "/usr/local/sbin/argin-taxpayer-backup-control",
    string sudoPath = "/usr/bin/sudo",
    bool useSudo = true)
    : IBackupHostCommandRunner, IBackupDatabaseSwitcher
{
    private static readonly HashSet<string> AllowedActions = ["stop-business", "start-business", "status-business", "activate-db"];

    public Task ActivateAsync(string databaseName, CancellationToken cancellationToken) =>
        RunWithValueAsync("activate-db", databaseName, cancellationToken);

    public async Task RunAsync(string action, CancellationToken cancellationToken) =>
        await RunCoreAsync(action, null, cancellationToken);

    public async Task RunWithValueAsync(string action, string value, CancellationToken cancellationToken) =>
        await RunCoreAsync(action, value, cancellationToken);

    private async Task RunCoreAsync(string action, string? value, CancellationToken cancellationToken)
    {
        if (!AllowedActions.Contains(action)) throw new ArgumentException("Unsupported service lifecycle action.", nameof(action));
        var start = new ProcessStartInfo(useSudo ? sudoPath : helperPath)
        { UseShellExecute = false, RedirectStandardError = true };
        if (useSudo)
        {
            start.ArgumentList.Add("-n");
            start.ArgumentList.Add(helperPath);
        }
        start.ArgumentList.Add(action);
        if (action == "activate-db")
        {
            if (value is null || !System.Text.RegularExpressions.Regex.IsMatch(value, "^recovery_[a-f0-9]{12}$"))
                throw new ArgumentException("Invalid isolated database name.", nameof(value));
            start.ArgumentList.Add(value);
        }
        else if (value is not null) throw new ArgumentException("Unexpected lifecycle argument.", nameof(value));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Backup host helper could not start.");
        var errorDrain = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var error = await errorDrain;
        if (process.ExitCode != 0)
            throw new BackupHostLifecycleException(action, process.ExitCode, string.IsNullOrWhiteSpace(error)
                ? "No diagnostic output; inspect the host helper log."
                : error[..Math.Min(error.Length, 1024)]);
    }
}

public sealed class ServiceLifecycle(IBackupHostCommandRunner commands)
{
    public Task StopBusinessServicesAsync(CancellationToken cancellationToken) =>
        commands.RunAsync("stop-business", cancellationToken);

    public async Task StartBusinessServicesAsync(CancellationToken cancellationToken)
    {
        await commands.RunAsync("start-business", cancellationToken);
        await commands.RunAsync("status-business", cancellationToken);
    }

    public Task VerifyBusinessServicesStoppedAsync(CancellationToken cancellationToken) =>
        commands.RunAsync("status-business", cancellationToken);
}

public interface IBackupApiReadinessProbe
{
    Task EnsureReadyAsync(CancellationToken cancellationToken);
}

public sealed class LoopbackBackupApiReadinessProbe : IBackupApiReadinessProbe
{
    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler { UseProxy = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        for (var attempt = 0; attempt < 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync("http://127.0.0.1:15090/health/ready", cancellationToken);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            if (attempt < 5) await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
        throw new InvalidOperationException("The API did not pass PostgreSQL readiness checks in recovery mode.");
    }
}
