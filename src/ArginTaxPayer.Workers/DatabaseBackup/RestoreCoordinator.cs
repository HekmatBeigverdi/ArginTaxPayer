using System.Text.Json;
using ArginTaxPayer.Domain.Administration;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using ArginTaxPayer.Infrastructure.Persistence;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public sealed record RestoreStage(Guid StageId, Guid ArtifactId, string DatabaseName,
    string SourceDatabase, string SchemaIdentity, DateTime StagedAtUtc, string Status);

public sealed class RestoreCoordinator(
    IConfiguration configuration,
    BackupOperationStore operations,
    RecoveryModeStore recovery,
    IBackupDatabaseSwitcher databaseSwitcher,
    ServiceLifecycle lifecycle,
    IBackupApiReadinessProbe readiness,
    ArginTaxPayerDbContext db,
    BackupExecutionService backupExecution)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string stateDirectory = configuration["BackupController:StateDirectory"]
        ?? "/var/lib/argintaxpayer/backup-controller";

    public async Task<RestoreStage> StageLocalAsync(Guid actor, Guid artifactId,
        DatabaseBackupSettings settings, CancellationToken ct, Guid? operationId = null,
        BackupProgressPlan? progressPlan = null, bool validationAlreadyReported = false)
    {
        var plan = progressPlan ?? BackupProgressPlan.ForLocalRestore();
        var operation = operationId is Guid existingId
            ? await RequireExistingOperationAsync(existingId, ct)
            : await operations.StartAsync("restore-stage", actor, "isolated", ct,
                plan.FirstStage, plan.TotalSteps);
        string? databaseName = null;
        try
        {
            if (!validationAlreadyReported)
                await plan.ReportStageAsync(operations, operation.Id, "validate-local", ct);
            var artifacts = await new LocalBackupStore(settings.LocalPath).ListAsync(ct);
            var artifact = artifacts.SingleOrDefault(x => x.ArtifactId == artifactId)
                ?? throw new FileNotFoundException("The selected local backup was not found.");
            if (!artifact.ArchiveValidated || !artifact.ChecksumMatches)
                throw new InvalidDataException("The selected backup failed integrity verification.");
            var currentConnection = configuration.GetConnectionString("ArginTaxPayerDb")
                ?? throw new InvalidOperationException("PostgreSQL connection is not configured.");
            var sourceDatabase = new NpgsqlConnectionStringBuilder(currentConnection).Database;
            var originalDatabase = configuration["BackupController:SourceDatabaseIdentity"] ?? sourceDatabase;
            var isPriorRecovery = System.Text.RegularExpressions.Regex.IsMatch(artifact.DatabaseName ?? "", "^recovery_[a-f0-9]{12}$");
            if ((!string.Equals(sourceDatabase, artifact.DatabaseName, StringComparison.Ordinal) &&
                 !string.Equals(originalDatabase, artifact.DatabaseName, StringComparison.Ordinal) && !isPriorRecovery) ||
                !string.Equals(configuration["BackupController:SchemaIdentity"] ?? "phase18", artifact.SchemaIdentity, StringComparison.Ordinal))
                throw new InvalidDataException("Backup database identity does not match the configured application database.");
            var keyPath = configuration["BackupController:EncryptionKeyPath"] ?? "/etc/argintaxpayer/database-backup.key";
            var key = await new FileBackupKeyProvider(keyPath).GetByVersionAsync(artifact.EncryptionKeyVersion, ct);
            try
            {
                await plan.ReportStageAsync(operations, operation.Id, "restore-isolated", ct);
                databaseName = "recovery_" + Guid.NewGuid().ToString("N")[..12];
                await using var archive = new FileStream(artifact.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var restoreConnection = configuration.GetConnectionString("DatabaseBackupRestore")
                    ?? throw new InvalidOperationException("A separate database restore credential is not configured.");
                await new PostgresRestoreDatabaseCommands(restoreConnection,
                    new NpgsqlConnectionStringBuilder(currentConnection).Username ?? "",
                    configuration["BackupController:PgRestorePath"] ?? "/usr/bin/pg_restore",
                    Path.Combine(stateDirectory, "tmp"))
                    .RestoreIsolatedAsync(archive, key, databaseName, ct);
                var stage = new RestoreStage(Guid.NewGuid(), artifactId, databaseName, sourceDatabase,
                    artifact.SchemaIdentity, DateTime.UtcNow, "staged");
                await WriteStageAsync(stage, ct);
                await recovery.SaveAsync(new RecoveryState(false, stage.StageId, stage.DatabaseName, null,
                    DateTime.UtcNow, "Backup restored to an isolated database and awaits review."), ct);
                await plan.ReportStageAsync(operations, operation.Id, "stage-review", ct);
                await operations.CompleteAsync(operation.Id, true, "Restore staged for review.", CancellationToken.None);
                return stage;
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(key.Material); }
        }
        catch (Exception ex)
        {
            if (databaseName is not null)
            {
                try
                {
                    var current = configuration.GetConnectionString("DatabaseBackupRestore")
                        ?? throw new InvalidOperationException("A separate database restore credential is not configured.");
                    await new PostgresRestoreDatabaseCommands(current,
                        new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("ArginTaxPayerDb")!).Username ?? "",
                        configuration["BackupController:PgRestorePath"] ?? "/usr/bin/pg_restore", Path.Combine(stateDirectory, "tmp"))
                        .DropIsolatedAsync(databaseName, CancellationToken.None);
                }
                catch { }
            }
            await operations.CompleteAsync(operation.Id, false, ex.GetType().Name, CancellationToken.None);
            throw;
        }
    }

    public async Task<RestoreStage> StageRemoteAsync(Guid actor, string remoteName,
        DatabaseBackupSettings settings, CancellationToken ct, Guid? operationId = null)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(remoteName,
            "^[0-9]{8}T[0-9]{9}Z_[a-f0-9]{32}\\.argonbackup$"))
            throw new ArgumentException("Invalid remote backup name.", nameof(remoteName));
        var plan = BackupProgressPlan.ForRemoteRestore();
        var operation = operationId is Guid existingId
            ? await RequireExistingOperationAsync(existingId, ct)
            : await operations.StartAsync("restore-stage", actor, "isolated", ct,
                plan.FirstStage, plan.TotalSteps);
        var tempRoot = Path.Combine(stateDirectory, "tmp", "remote-restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var delegatedToLocalStage = false;
        try
        {
            var archivePath = Path.Combine(tempRoot, remoteName);
            var manifestName = Path.ChangeExtension(remoteName, null) + ".manifest.json";
            var manifestPath = Path.Combine(tempRoot, manifestName);
            await plan.ReportStageAsync(operations, operation.Id, "download-remote", ct);
            await backupExecution.DownloadRemoteAsync(settings, remoteName, archivePath, ct);
            await backupExecution.DownloadRemoteAsync(settings, manifestName, manifestPath, ct);
            await plan.ReportStageAsync(operations, operation.Id, "validate-remote", ct);
            BackupManifest manifest;
            await using (var input = File.OpenRead(manifestPath))
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(input, JsonOptions, ct)
                    ?? throw new InvalidDataException("Remote backup manifest is invalid.");
            var expectedId = Guid.ParseExact(remoteName.Split('_')[1][..32], "N");
            var info = new FileInfo(archivePath);
            await using var hashInput = File.OpenRead(archivePath);
            var hash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(hashInput, ct));
            if (manifest.ArtifactId != expectedId || !manifest.ArchiveValidated ||
                manifest.EncryptedBytes != info.Length || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(manifest.Sha256), Convert.FromHexString(hash)))
                throw new InvalidDataException("Remote backup integrity verification failed.");
            var temporaryStore = new LocalBackupStore(tempRoot + "/verified");
            await using var archive = File.OpenRead(archivePath);
            var verified = await temporaryStore.PublishAsync(archive, manifest, ct);
            delegatedToLocalStage = true;
            return await StageLocalAsync(actor, verified.ArtifactId,
                settings with { LocalPath = tempRoot + "/verified" }, ct, operation.Id,
                plan, validationAlreadyReported: true);
        }
        catch (Exception ex)
        {
            if (!delegatedToLocalStage)
                await operations.CompleteAsync(operation.Id, false, ex.GetType().Name, CancellationToken.None);
            throw;
        }
        finally { try { Directory.Delete(tempRoot, true); } catch (IOException) { } }
    }

    public async Task<RestoreStage> ActivateAsync(Guid actor, Guid stageId, DatabaseBackupSettings settings, string confirmation,
        CancellationToken ct, Guid? operationId = null)
    {
        if (!string.Equals(confirmation, "ACTIVATE_RESTORE", StringComparison.Ordinal))
            throw new ArgumentException("Explicit restore activation confirmation is required.", nameof(confirmation));
        var plan = BackupProgressPlan.ForActivation();
        var operation = operationId is Guid existingId
            ? await RequireExistingOperationAsync(existingId, ct)
            : await operations.StartAsync("restore-activate", actor, "recovery-only", ct,
                plan.FirstStage, plan.TotalSteps);
        try
        {
            await plan.ReportStageAsync(operations, operation.Id, "pre-restore-backup", ct);
            var stage = await ReadStageAsync(ct);
            var currentState = await recovery.GetAsync(ct);
            if (stage is null || stage.StageId != stageId || stage.Status != "staged" || currentState.RecoveryMode)
                throw new InvalidOperationException("The selected restore stage is not available for activation.");
            var connection = configuration.GetConnectionString("ArginTaxPayerDb")
                ?? throw new InvalidOperationException("PostgreSQL connection is not configured.");
            var previousDatabase = new NpgsqlConnectionStringBuilder(connection).Database;
            // Preserve the current live database as a verified encrypted backup before changing selection.
            await backupExecution.CreateAsync(actor, "pre-restore", settings, stopBusinessServices: false, ct,
                operation.Id, completeOperation: false, reportProgress: false);
            await plan.ReportStageAsync(operations, operation.Id, "stop-services", ct);
            await lifecycle.StopBusinessServicesAsync(ct);
            await recovery.SaveAsync(new RecoveryState(true, stage.StageId, stage.DatabaseName, previousDatabase,
                DateTime.UtcNow, "Database switched to a staged restore; business APIs remain disabled."), ct);
            await plan.ReportStageAsync(operations, operation.Id, "switch-database", ct);
            await databaseSwitcher.ActivateAsync(stage.DatabaseName, ct);
            await plan.ReportStageAsync(operations, operation.Id, "activate-recovery", ct);
            await operations.CompleteAsync(operation.Id, true,
                "Restore activated in recovery-only mode. Review it before enabling the application.", CancellationToken.None);
            return stage with { Status = "recovery-only" };
        }
        catch (Exception ex)
        {
            await operations.CompleteAsync(operation.Id, false, ex.GetType().Name, CancellationToken.None);
            throw;
        }
    }

    private async Task<BackupOperationRecord> RequireExistingOperationAsync(Guid operationId, CancellationToken ct)
    {
        var operation = await operations.GetStatusAsync(ct);
        return operation is { Status: "running" } && operation.Id == operationId
            ? operation
            : throw new InvalidOperationException("The queued restore operation is no longer active.");
    }

    public async Task EnableBusinessModeAsync(string confirmation, CancellationToken ct)
    {
        if (!string.Equals(confirmation, "ENABLE_BUSINESS_OPERATIONS", StringComparison.Ordinal))
            throw new ArgumentException("Explicit business re-enable confirmation is required.", nameof(confirmation));
        var state = await recovery.GetAsync(ct);
        if (!state.RecoveryMode || state.StageId is null)
            throw new InvalidOperationException("The application is not in restore review mode.");
        var selectedDatabase = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("ArginTaxPayerDb") ?? "").Database;
        if (!string.Equals(selectedDatabase, state.ActiveDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("Wait for the backup controller to reconnect to the restored database before enabling business operations.");
        var expectedMigrations = db.Database.GetMigrations().ToArray();
        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        if (expectedMigrations.Length == 0 || !DatabaseSchemaCompatibility.Matches(expectedMigrations, appliedMigrations))
            throw new InvalidOperationException("Restored database migrations do not match the deployed application schema.");
        await lifecycle.StartBusinessServicesAsync(ct);
        await readiness.EnsureReadyAsync(ct);
        await recovery.SaveAsync(state with { RecoveryMode = false, UpdatedAtUtc = DateTime.UtcNow,
            Message = "Business operations were explicitly re-enabled by a Level 1 administrator." }, ct);
    }

    public Task<RecoveryState> GetRecoveryStateAsync(CancellationToken ct) => recovery.GetAsync(ct);

    private async Task WriteStageAsync(RestoreStage stage, CancellationToken ct)
    {
        Directory.CreateDirectory(stateDirectory);
        var path = Path.Combine(stateDirectory, "restore-stage.json");
        var temporary = path + ".partial";
        await using (var output = new FileStream(temporary, new FileStreamOptions
        {
            Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        }))
        {
            await JsonSerializer.SerializeAsync(output, stage, JsonOptions, ct);
            await output.FlushAsync(ct); output.Flush(true);
        }
        File.Move(temporary, path, true);
    }

    private async Task<RestoreStage?> ReadStageAsync(CancellationToken ct)
    {
        var path = Path.Combine(stateDirectory, "restore-stage.json");
        if (!File.Exists(path)) return null;
        await using var input = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RestoreStage>(input, JsonOptions, ct);
    }
}

public static class DatabaseSchemaCompatibility
{
    // This migration adds only a backup-settings CHECK constraint; it has no business schema changes.
    private const string BackupControlMetadataMigration = "20261115130000_AddDatabaseBackupControl";

    public static bool Matches(IEnumerable<string> expected, IEnumerable<string> applied)
    {
        static string[] BusinessMigrations(IEnumerable<string> values) => values
            .Where(value => !value.EndsWith(BackupControlMetadataMigration, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        var required = BusinessMigrations(expected);
        return required.Length > 0 && required.SequenceEqual(BusinessMigrations(applied), StringComparer.Ordinal);
    }
}
