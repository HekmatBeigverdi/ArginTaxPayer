using ArginTaxPayer.Domain.Administration;
using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class BackupExecutionLifecycleTests
{
    [Fact]
    public async Task Pre_restore_backup_reuses_outer_operation_without_releasing_its_lock()
    {
        var root = TemporaryRoot();
        try
        {
            var operations = new BackupOperationStore(root);
            var operation = await operations.StartAsync("restore-activate", Guid.NewGuid(), "recovery-only", CancellationToken.None);
            var service = CreateService(root, operations, new FakeArtifactCreator());

            await service.CreateAsync(Guid.NewGuid(), "pre-restore", DatabaseBackupSettings.Default,
                stopBusinessServices: false, CancellationToken.None, operation.Id, completeOperation: false);

            Assert.Equal("running", (await operations.GetStatusAsync(CancellationToken.None))!.Status);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Manual_backup_enters_maintenance_without_stopping_api_or_auto_reactivating()
    {
        var root = TemporaryRoot();
        try
        {
            var operations = new BackupOperationStore(root);
            var runner = new RecordingRunner();
            var manualStop = new ManualServiceStopState(root);
            var service = CreateService(root, operations, new FakeArtifactCreator(), runner, manualStop);

            await service.CreateAsync(Guid.NewGuid(), "manual", DatabaseBackupSettings.Default,
                stopBusinessServices: true, CancellationToken.None);

            Assert.Empty(runner.Actions);
            Assert.True((await manualStop.GetAsync(CancellationToken.None)).RestartRequired);
            Assert.Equal("succeeded", (await operations.GetStatusAsync(CancellationToken.None))!.Status);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task Failed_manual_backup_keeps_maintenance_until_manual_reactivation()
    {
        var root = TemporaryRoot();
        try
        {
            var operations = new BackupOperationStore(root);
            var runner = new RecordingRunner();
            var manualStop = new ManualServiceStopState(root);
            var service = CreateService(root, operations, new ThrowingArtifactCreator(), runner, manualStop);

            await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(Guid.NewGuid(), "manual",
                DatabaseBackupSettings.Default, stopBusinessServices: true, CancellationToken.None));

            Assert.Empty(runner.Actions);
            Assert.True((await manualStop.GetAsync(CancellationToken.None)).RestartRequired);
            Assert.Equal("failed", (await operations.GetStatusAsync(CancellationToken.None))!.Status);
        }
        finally { DeleteRoot(root); }
    }

    private static BackupExecutionService CreateService(string root, BackupOperationStore operations,
        IBackupArtifactCreator creator, RecordingRunner? runner = null, ManualServiceStopState? stopState = null) =>
        new(creator, operations, new FtpsCredentialStore(Path.Combine(root, "ftps.json")),
            new ServiceLifecycle(runner ?? new RecordingRunner()), stopState ?? new ManualServiceStopState(root));

    private static string TemporaryRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteRoot(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }

    private sealed class FakeArtifactCreator : IBackupArtifactCreator
    {
        public Task<BackupArtifactMetadata> CreateAsync(DatabaseBackupSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(new BackupArtifactMetadata(Guid.NewGuid(), DateTime.UtcNow, "db", "phase18", "1",
                32, "AABB", true, true, "/backup/file", "/backup/manifest"));
    }

    private sealed class ThrowingArtifactCreator : IBackupArtifactCreator
    {
        public Task<BackupArtifactMetadata> CreateAsync(DatabaseBackupSettings settings, CancellationToken cancellationToken) =>
            throw new IOException("fake failure");
    }

    private sealed class RecordingRunner : IBackupHostCommandRunner
    {
        public List<string> Actions { get; } = [];
        public Task RunAsync(string action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }
}
