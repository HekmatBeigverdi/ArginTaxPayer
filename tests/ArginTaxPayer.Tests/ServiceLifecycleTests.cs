using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ServiceLifecycleTests
{
    [Fact]
    public async Task Lifecycle_uses_only_fixed_business_api_actions()
    {
        var runner = new RecordingHostRunner();
        var lifecycle = new ServiceLifecycle(runner);
        await lifecycle.StopBusinessServicesAsync(CancellationToken.None);
        await lifecycle.StartBusinessServicesAsync(CancellationToken.None);
        Assert.Equal(new[] { "stop-business", "start-business", "status-business" }, runner.Actions);
        Assert.DoesNotContain("argintaxpayer-web.service", runner.Actions);
    }

    [Fact]
    public async Task Host_runner_rejects_forged_action_before_process_start()
    {
        var runner = new ProcessBackupHostCommandRunner("/not-used", "/not-used");
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync("start-anything.service", CancellationToken.None));
    }

    private sealed class RecordingHostRunner : IBackupHostCommandRunner
    {
        public List<string> Actions { get; } = [];
        public Task RunAsync(string action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }
}
