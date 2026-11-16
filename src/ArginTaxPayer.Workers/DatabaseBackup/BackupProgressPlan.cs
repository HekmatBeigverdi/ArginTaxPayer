using ArginTaxPayer.Domain.Administration;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record BackupProgressPlan(IReadOnlyList<string> Stages)
{
    public int TotalSteps => Stages.Count;
    public string FirstStage => Stages[0];

    public Task<bool> ReportStageAsync(BackupOperationStore operations, Guid operationId,
        string stage, CancellationToken cancellationToken)
    {
        var completedSteps = -1;
        for (var index = 0; index < Stages.Count; index++)
        {
            if (!string.Equals(Stages[index], stage, StringComparison.Ordinal)) continue;
            completedSteps = index;
            break;
        }
        if (completedSteps < 0)
            throw new ArgumentException("Stage is not part of the active backup progress plan.", nameof(stage));
        return operations.ReportProgressAsync(operationId, stage, completedSteps, TotalSteps, cancellationToken);
    }

    public static BackupProgressPlan ForBackup(bool stopBusinessServices, bool remoteEnabled)
    {
        var stages = new List<string>();
        if (stopBusinessServices) stages.Add("stop-services");
        stages.AddRange(["prepare-backup", "dump-encrypt", "verify-archive", "publish-local"]);
        if (remoteEnabled) stages.Add("publish-remote");
        return new BackupProgressPlan(stages);
    }

    public static BackupProgressPlan ForLocalRestore() =>
        new(["validate-local", "restore-isolated", "stage-review"]);

    public static BackupProgressPlan ForRemoteRestore() =>
        new(["download-remote", "validate-remote", "restore-isolated", "stage-review"]);

    public static BackupProgressPlan ForImport() =>
        new(["validate-bundle", "verify-upload", "publish-import"]);

    public static BackupProgressPlan ForActivation() =>
        new(["pre-restore-backup", "stop-services", "switch-database", "activate-recovery"]);

    public static BackupProgressPlan ForQueued(BackupWorkItem item) => item.Action switch
    {
        "import-zip" => ForImport(),
        "manual" => ForBackup(stopBusinessServices: true, item.Settings.RemoteEnabled),
        "restore-local" => ForLocalRestore(),
        "restore-remote" => ForRemoteRestore(),
        "restore-activate" => ForActivation(),
        _ => throw new InvalidOperationException("A progress plan is not defined for this backup operation.")
    };
}
