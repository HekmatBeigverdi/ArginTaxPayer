using System.Threading.Channels;
using ArginTaxPayer.Domain.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record BackupWorkItem(string Action, Guid OperationId, Guid ActorId,
    DatabaseBackupSettings Settings, Guid? ArtifactId = null, Guid? StageId = null, string? RemoteName = null, string? Confirmation = null, string? BundlePath = null);

public sealed class BackupWorkQueue
{
    private readonly Channel<BackupWorkItem> channel = Channel.CreateBounded<BackupWorkItem>(
        new BoundedChannelOptions(4) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });

    public bool TryEnqueue(BackupWorkItem item) => channel.Writer.TryWrite(item);
    public IAsyncEnumerable<BackupWorkItem> ReadAllAsync(CancellationToken ct) => channel.Reader.ReadAllAsync(ct);
}

public sealed class BackupWorkService(
    BackupWorkQueue queue,
    IServiceScopeFactory scopes,
    BackupOperationStore operations,
    ILogger<BackupWorkService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (!await operations.MarkRunningAsync(item.OperationId, stoppingToken))
                    throw new InvalidOperationException("The queued backup operation is no longer active.");
                using var scope = scopes.CreateScope();
                var execution = scope.ServiceProvider.GetRequiredService<BackupExecutionService>();
                var restore = scope.ServiceProvider.GetRequiredService<RestoreCoordinator>();
                switch (item.Action)
                {
                    case "import-zip":
                        await scope.ServiceProvider.GetRequiredService<BackupBundleService>().ImportAsync(item.BundlePath!, item.Settings, operations, item.OperationId, stoppingToken);
                        break;
                    case "manual":
                        await execution.CreateAsync(item.ActorId, "manual", item.Settings, true, stoppingToken, item.OperationId);
                        break;
                    case "restore-local":
                        await restore.StageLocalAsync(item.ActorId, item.ArtifactId!.Value, item.Settings, stoppingToken, item.OperationId);
                        break;
                    case "restore-remote":
                        await restore.StageRemoteAsync(item.ActorId, item.RemoteName!, item.Settings, stoppingToken, item.OperationId);
                        break;
                    case "restore-activate":
                        await restore.ActivateAsync(item.ActorId, item.StageId!.Value, item.Settings, item.Confirmation ?? "", stoppingToken, item.OperationId);
                        break;
                    default: throw new InvalidOperationException("Unsupported backup operation.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                var failedOperation = await operations.GetStatusAsync(CancellationToken.None);
                logger.LogError("Backup operation {OperationId} action {Action} failed at stage {Stage} ({ErrorType}, inner {InnerType}). Detail: {Detail}. Stack: {Stack}",
                    item.OperationId, item.Action, failedOperation?.CurrentStage, ex.GetType().Name, ex.InnerException?.GetType().Name,
                    ex is BackupHostLifecycleException or BackupBundleException ? ex.Message : "Sensitive exception contents omitted.", ex.StackTrace);
                try { await operations.CompleteAsync(item.OperationId, false,
                    ex is BackupBundleException ? ex.Message : "Operation failed (" + ex.GetType().Name + ").", CancellationToken.None); }
                catch (InvalidOperationException) { }
            }
            finally
            {
                if (item.BundlePath is not null) File.Delete(item.BundlePath);
            }
        }
    }
}
