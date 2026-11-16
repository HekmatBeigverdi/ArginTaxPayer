using ArginTaxPayer.Workers.DatabaseBackup;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ManualServiceStopStateTests
{
    [Fact]
    public async Task Restart_requirement_survives_new_operations_and_controller_restart_until_admin_clears_it()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var operationId = Guid.NewGuid();
            await new ManualServiceStopState(path).MarkRestartRequiredAsync(operationId, CancellationToken.None);
            var restartedController = new ManualServiceStopState(path);
            var state = await restartedController.GetAsync(CancellationToken.None);
            Assert.True(state.RestartRequired);
            Assert.Equal(operationId, state.OperationId);
            await restartedController.ClearAsync(CancellationToken.None);
            Assert.False((await new ManualServiceStopState(path).GetAsync(CancellationToken.None)).RestartRequired);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
