using System.Text.Json;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record BackupOperationRecord(Guid Id, string Kind, string ActorId, string Mode,
    string Status, DateTime StartedAtUtc, DateTime? CompletedAtUtc, string? Message)
{
    public string? CurrentStage { get; init; }
    public int CompletedSteps { get; init; }
    public int TotalSteps { get; init; }
    public int ProgressPercent => Status == "succeeded" ? 100 : TotalSteps <= 0
        ? 0 : Math.Clamp((int)((long)CompletedSteps * 100 / TotalSteps), 0, 99);
}

/// <summary>Controller-owned operation state that is not replaced with a restored database.</summary>
public sealed class BackupOperationStore(string stateDirectory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string root = Path.GetFullPath(stateDirectory);
    private string StatePath => Path.Combine(root, "operation.json");
    private string LockPath => Path.Combine(root, "operation.lock");

    public async Task<BackupOperationRecord> StartAsync(string kind, Guid actorId, string mode,
        CancellationToken cancellationToken, string? currentStage = null, int totalSteps = 0,
        bool queued = false)
    {
        if (string.IsNullOrWhiteSpace(kind) || (actorId == Guid.Empty && kind != "scheduled") || string.IsNullOrWhiteSpace(mode))
            throw new ArgumentException("A backup operation kind, actor and mode are required.");
        ValidateInitialProgress(currentStage, totalSteps);
        Directory.CreateDirectory(root);
        await using var operationLock = await AcquireAsync(cancellationToken);
        var current = await ReadAsync(cancellationToken);
        if (current?.Status is "running" or "queued") throw new InvalidOperationException("A backup operation is already running or queued.");
        var operation = new BackupOperationRecord(Guid.NewGuid(), kind, actorId == Guid.Empty ? "system" : actorId.ToString("N"), mode,
            queued ? "queued" : "running", DateTime.UtcNow, null, null)
        {
            CurrentStage = currentStage,
            TotalSteps = totalSteps
        };
        await WriteAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<bool> MarkRunningAsync(Guid operationId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        await using var operationLock = await AcquireAsync(cancellationToken);
        var current = await ReadAsync(cancellationToken);
        if (current is null || current.Id != operationId || current.Status != "queued") return false;
        await WriteAsync(current with { Status = "running" }, cancellationToken);
        return true;
    }

    public async Task<bool> ReportProgressAsync(Guid operationId, string stage, int completedSteps,
        int totalSteps, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(stage) || stage.Length > 128)
            throw new ArgumentException("A valid operation ID and stage are required.");
        if (totalSteps is < 1 or > 32 || completedSteps < 0 || completedSteps >= totalSteps)
            throw new ArgumentOutOfRangeException(nameof(completedSteps), "Progress must describe an active stage before completion.");

        Directory.CreateDirectory(root);
        await using var operationLock = await AcquireAsync(cancellationToken);
        var current = await ReadAsync(cancellationToken);
        if (current is null || current.Id != operationId || current.Status != "running")
            return false;
        if (current.TotalSteps != totalSteps || completedSteps < current.CompletedSteps)
            throw new InvalidOperationException("Backup operation progress plan cannot change or move backwards.");

        await WriteAsync(current with { CurrentStage = stage, CompletedSteps = completedSteps }, cancellationToken);
        return true;
    }

    public async Task<BackupOperationRecord?> GetStatusAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        return await ReadAsync(cancellationToken);
    }

    public async Task<BackupOperationRecord?> RecoverInterruptedAsync(CancellationToken cancellationToken, bool recoveryMode = false)
    {
        Directory.CreateDirectory(root);
        await using var operationLock = await AcquireAsync(cancellationToken);
        var current = await ReadAsync(cancellationToken);
        if (current?.Status is not ("running" or "queued")) return current;
        var activationCompleted = current.Status == "running" && recoveryMode && current.Kind == "restore-activate";
        var recovered = current with
        {
            Status = activationCompleted ? "succeeded" : "failed",
            CompletedAtUtc = DateTime.UtcNow,
            Message = activationCompleted
                ? "Restore is active in recovery-only mode; review it before enabling business operations."
                : "Controller restarted while this operation was running; verify services and artifacts before retrying."
        };
        await WriteAsync(recovered, cancellationToken);
        return recovered;
    }

    public async Task<BackupOperationRecord> CompleteAsync(Guid operationId, bool success, string? message,
        CancellationToken cancellationToken)
    {
        if (message?.Length > 512) message = message[..512];
        Directory.CreateDirectory(root);
        await using var operationLock = await AcquireAsync(cancellationToken);
        var current = await ReadAsync(cancellationToken);
        if (current is null || current.Id != operationId ||
            (current.Status != "running" && !(current.Status == "queued" && !success)))
            throw new InvalidOperationException("The active backup operation could not be found.");
        var completed = current with
        {
            Status = success ? "succeeded" : "failed",
            CompletedAtUtc = DateTime.UtcNow,
            Message = message,
            CurrentStage = current.CurrentStage,
            CompletedSteps = success && current.TotalSteps > 0 ? current.TotalSteps : current.CompletedSteps
        };
        await WriteAsync(completed, cancellationToken);
        return completed;
    }

    private async Task<FileStream> AcquireAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1); }
            catch (IOException) { await Task.Delay(100, cancellationToken); }
        }
    }

    private static void ValidateInitialProgress(string? currentStage, int totalSteps)
    {
        if (totalSteps == 0 && currentStage is null) return;
        if (totalSteps is < 1 or > 32 || string.IsNullOrWhiteSpace(currentStage) || currentStage.Length > 128)
            throw new ArgumentException("An initial progress stage and a valid total are required.");
    }

    private async Task<BackupOperationRecord?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StatePath)) return null;
        await using var input = File.OpenRead(StatePath);
        return await JsonSerializer.DeserializeAsync<BackupOperationRecord>(input, JsonOptions, cancellationToken);
    }

    private async Task WriteAsync(BackupOperationRecord operation, CancellationToken cancellationToken)
    {
        var partial = StatePath + ".partial";
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(output, operation, JsonOptions, cancellationToken);
            await output.FlushAsync(cancellationToken);
            output.Flush(true);
        }
        File.Move(partial, StatePath, true);
    }
}
