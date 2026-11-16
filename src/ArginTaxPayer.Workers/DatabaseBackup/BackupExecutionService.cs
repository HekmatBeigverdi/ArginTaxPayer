using ArginTaxPayer.Domain.Administration;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Workers.DatabaseBackup;

public interface IBackupArtifactCreator
{
    Task<BackupArtifactMetadata> CreateAsync(DatabaseBackupSettings settings, CancellationToken cancellationToken);
}

public interface IProgressBackupArtifactCreator : IBackupArtifactCreator
{
    Task<BackupArtifactMetadata> CreateWithProgressAsync(DatabaseBackupSettings settings,
        BackupProgressPlan plan, BackupOperationStore operations, Guid operationId,
        CancellationToken cancellationToken);
}

public sealed class BackupExecutionService(
    IBackupArtifactCreator artifactCreator,
    BackupOperationStore operations,
    FtpsCredentialStore credentials,
    ServiceLifecycle lifecycle,
    ManualServiceStopState manualStopState)
{
    public async Task<BackupArtifactMetadata> CreateAsync(Guid actorId, string kind,
        DatabaseBackupSettings settings, bool stopBusinessServices, CancellationToken cancellationToken,
        Guid? operationId = null, bool completeOperation = true, bool reportProgress = true)
    {
        var plan = reportProgress ? BackupProgressPlan.ForBackup(stopBusinessServices, settings.RemoteEnabled) : null;
        var operation = operationId is Guid existingId
            ? await RequireExistingOperationAsync(existingId, cancellationToken)
            : await operations.StartAsync(kind, actorId,
                stopBusinessServices ? "offline-required" : "online", cancellationToken,
                plan?.FirstStage, plan?.TotalSteps ?? 0);
        try
        {
            if (stopBusinessServices)
            {
                await manualStopState.MarkRestartRequiredAsync(operation.Id, cancellationToken);
                await lifecycle.StopBusinessServicesAsync(cancellationToken);
                if (plan is not null)
                    await plan.ReportStageAsync(operations, operation.Id, "prepare-backup", cancellationToken);
            }
            var artifact = plan is not null && artifactCreator is IProgressBackupArtifactCreator progressCreator
                ? await progressCreator.CreateWithProgressAsync(settings, plan, operations, operation.Id, cancellationToken)
                : await artifactCreator.CreateAsync(settings, cancellationToken);
            if (settings.RemoteEnabled)
            {
                if (plan is not null)
                    await plan.ReportStageAsync(operations, operation.Id, "publish-remote", cancellationToken);
                var secret = await credentials.ReadAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Remote FTPS credentials are not configured.");
                var remote = new RemoteBackupStore(
                    new ExplicitFtpsClient(new FtpsConnectionSettings(settings.FtpHost, settings.FtpPort,
                        secret.Username, secret.Password)), settings.RemoteDirectory);
                if (!string.Equals(secret.Username, settings.FtpUsername, StringComparison.Ordinal))
                    throw new InvalidOperationException("FTPS username changed; save the remote password again.");
                await remote.PublishAsync(artifact, cancellationToken);
                await remote.ApplyRetentionAsync(DateTime.UtcNow, TimeSpan.FromDays(7),
                    settings.MonthlyRetentionCount, cancellationToken);
            }
            if (completeOperation)
                await operations.CompleteAsync(operation.Id, true,
                    stopBusinessServices ? "Backup complete. Business API remains stopped until an administrator starts it." : null,
                    CancellationToken.None);
            return artifact;
        }
        catch (Exception ex)
        {
            if (completeOperation)
                await operations.CompleteAsync(operation.Id, false, "Backup failed (" + ex.GetType().Name + ").", CancellationToken.None);
            throw;
        }
    }

    private async Task<BackupOperationRecord> RequireExistingOperationAsync(Guid operationId, CancellationToken ct)
    {
        var operation = await operations.GetStatusAsync(ct);
        return operation is { Status: "running" } && operation.Id == operationId
            ? operation
            : throw new InvalidOperationException("The queued backup operation is no longer active.");
    }

    public async Task DownloadRemoteAsync(DatabaseBackupSettings settings, string name, string localPath,
        CancellationToken cancellationToken)
    {
        var secret = await credentials.ReadAsync(cancellationToken)
            ?? throw new InvalidOperationException("Remote FTPS credentials are not configured.");
        if (!string.Equals(secret.Username, settings.FtpUsername, StringComparison.Ordinal))
            throw new InvalidOperationException("FTPS username changed; save the remote password again.");
        var remote = new RemoteBackupStore(new ExplicitFtpsClient(new FtpsConnectionSettings(
            settings.FtpHost, settings.FtpPort, secret.Username, secret.Password)), settings.RemoteDirectory);
        await remote.DownloadAsync(name, localPath, cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteBackupMetadata>> ListRemoteAsync(DatabaseBackupSettings settings,
        CancellationToken cancellationToken)
    {
        var secret = await credentials.ReadAsync(cancellationToken)
            ?? throw new InvalidOperationException("Remote FTPS credentials are not configured.");
        if (!string.Equals(secret.Username, settings.FtpUsername, StringComparison.Ordinal))
            throw new InvalidOperationException("FTPS username changed; save the remote password again.");
        var remote = new RemoteBackupStore(new ExplicitFtpsClient(new FtpsConnectionSettings(
            settings.FtpHost, settings.FtpPort, secret.Username, secret.Password)), settings.RemoteDirectory);
        return await remote.ListAsync(cancellationToken);
    }

    public async Task StartBusinessServicesAsync(CancellationToken cancellationToken)
    {
        await lifecycle.StartBusinessServicesAsync(cancellationToken);
        await manualStopState.ClearAsync(cancellationToken);
    }

}

public sealed class PostgresBackupArtifactCreator(IConfiguration configuration) : IProgressBackupArtifactCreator
{
    public Task<BackupArtifactMetadata> CreateAsync(DatabaseBackupSettings settings, CancellationToken cancellationToken)
        => CreateAsync(settings, null, null, null, cancellationToken);

    public Task<BackupArtifactMetadata> CreateWithProgressAsync(DatabaseBackupSettings settings,
        BackupProgressPlan plan, BackupOperationStore operations, Guid operationId,
        CancellationToken cancellationToken) =>
        CreateAsync(settings, plan, operations, operationId, cancellationToken);

    private Task<BackupArtifactMetadata> CreateAsync(DatabaseBackupSettings settings,
        BackupProgressPlan? plan, BackupOperationStore? operations, Guid? operationId,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("ArginTaxPayerDb")
            ?? throw new InvalidOperationException("PostgreSQL connection is not configured.");
        var reader = configuration.GetConnectionString("DatabaseBackupRead");
        if (!string.IsNullOrWhiteSpace(reader))
        {
            var application = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            var backup = new Npgsql.NpgsqlConnectionStringBuilder(reader);
            if (!string.Equals(application.Host, backup.Host, StringComparison.OrdinalIgnoreCase) || application.Port != backup.Port)
                throw new InvalidOperationException("Backup reader must use the application PostgreSQL host and port.");
            // Preserve the selected active database after a restore, regardless of the
            // original database recorded in the private credential file.
            backup.Database = application.Database;
            connectionString = backup.ConnectionString;
        }
        var statePath = configuration["BackupController:StateDirectory"] ?? "/var/lib/argintaxpayer/backup-controller";
        var protectedTemp = Path.Combine(statePath, "tmp");
        return new PostgresBackupRunner(
            new LocalBackupStore(settings.LocalPath),
            new BackupRunnerOptions(connectionString, configuration["BackupController:SchemaIdentity"] ?? "phase18"),
            new NpgsqlBackupDatabaseSizeReader(connectionString),
            new DriveBackupStorageCapacity(),
            new FileBackupKeyProvider(configuration["BackupController:EncryptionKeyPath"] ?? "/etc/argintaxpayer/database-backup.key"),
            new PostgresBackupCommandRunner(new BackupRunnerOptions(connectionString,
                configuration["BackupController:SchemaIdentity"] ?? "phase18"),
                configuration["BackupController:PgDumpPath"] ?? "/usr/bin/pg_dump",
                configuration["BackupController:PgRestorePath"] ?? "/usr/bin/pg_restore", protectedTemp),
            plan, operations, operationId)
            .CreateAsync(cancellationToken);
    }
}
