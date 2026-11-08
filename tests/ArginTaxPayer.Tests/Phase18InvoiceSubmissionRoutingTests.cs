using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18InvoiceSubmissionRoutingTests
{
    [Theory]
    [InlineData("Production", true, false, true)]
    [InlineData("Sandbox", false, true, true)]
    [InlineData("Production", false, true, false)]
    [InlineData("Sandbox", true, false, true)]
    [InlineData("Production", true, true, false)]
    [InlineData("Sandbox", false, false, false)]
    [InlineData("Unknown", true, false, false)]
    public void Snapshot_memory_selection_rejects_missing_dual_and_cross_environment_bindings(
        string environment, bool production, bool sandbox, bool expected)
    {
        var memory = Guid.NewGuid();
        var snapshot = JsonSerializer.Serialize(new {
            environment,
            fiscalMemoryId = production ? (Guid?)memory : null,
            sandboxFiscalMemoryId = sandbox ? (Guid?)memory : null
        });
        Assert.Equal(expected, Phase18InvoiceMemoryBindingService.TryReadMemory(
            snapshot, out var selected, out var legacySandbox));
        if (expected) {
            Assert.Equal(memory, selected);
            Assert.Equal(sandbox, legacySandbox);
        }
    }

    [Theory]
    [InlineData("Acknowledged", "already_acknowledged")]
    [InlineData("Indeterminate", "existing_dispatch_requires_reconciliation")]
    [InlineData("Sending", "existing_dispatch_requires_reconciliation")]
    [InlineData("FailedTerminal", "existing_dispatch_requires_reconciliation")]
    [InlineData("RetryEligible", "existing_dispatch_requires_reconciliation")]
    [InlineData("Signed", "existing_dispatch_requires_reconciliation")]
    public void Existing_attempts_return_receipt_or_reconciliation_without_reentry(
        string status, string code)
    {
        var overview = new InvoiceDispatchOverview(Guid.NewGuid(), Guid.NewGuid(), 1,
            status, 4, DateTime.UtcNow, "Production", false, false, false, "ref-123");
        var result = Phase18InvoiceSubmissionService.ExistingDispatchResult(overview);
        Assert.NotNull(result);
        Assert.Equal(code, result.Code);
        Assert.Equal(status, result.Status);
        Assert.Equal("Production", result.Environment);
        if (status == "Acknowledged") Assert.Equal("ref-123", result.ReferenceNumber);
    }

    [Theory]
    [InlineData("NotPrepared")]
    [InlineData("Prepared")]
    [InlineData("Reserved")]
    public void Only_unattempted_operations_can_enter_preparation(string state)
    {
        Assert.Null(Phase18InvoiceSubmissionService.ExistingDispatchResult(new(
            Guid.NewGuid(), Guid.NewGuid(), 1, state, 1, DateTime.UtcNow,
            "Production", false, false, false)));
    }

    [Fact]
    public void Production_snapshot_validation_requires_exclusive_real_memory()
    {
        var id = Guid.NewGuid();
        string Snapshot(Guid? sandbox = null) => JsonSerializer.Serialize(new {
            sourceWorkspaceId = id, sourceRevision = 1, environment = "Production",
            fiscalMemoryId = Guid.NewGuid(), sandboxFiscalMemoryId = sandbox,
            buyerPartyId = Guid.NewGuid(), lines = new[] { new { quantity = 1 } },
            settlementMethod = 1, financialSummary = new { total = 1 }
        });
        Assert.True(Phase10InternalInvoiceValidationService.ValidSnapshot(
            Snapshot(), new string('a', 64), id));
        Assert.False(Phase10InternalInvoiceValidationService.ValidSandboxSnapshot(
            Snapshot(), new string('a', 64), id));
        Assert.False(Phase10InternalInvoiceValidationService.ValidSnapshot(
            Snapshot(Guid.NewGuid()), new string('a', 64), id));
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"environment\":\"Production\",\"fiscalMemoryId\":0}")]
    [InlineData("{\"environment\":\"Production\",\"fiscalMemoryId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"environment\":\"Sandbox\",\"fiscalMemoryId\":false,\"sandboxFiscalMemoryId\":\"c2bb3b75-05ec-4850-b683-01d730985a63\"}")]
    public void Malformed_and_nonexclusive_memory_values_fail_closed(string snapshot)
    {
        Assert.False(Phase18InvoiceMemoryBindingService.TryReadMemory(snapshot, out _, out _));
    }

    [Fact]
    public void Duplicate_memory_selection_properties_are_rejected()
    {
        var id = Guid.NewGuid();
        var raw = $"{{\"environment\":\"Sandbox\",\"environment\":\"Production\",\"fiscalMemoryId\":\"{id}\"}}";
        Assert.False(Phase18InvoiceMemoryBindingService.TryReadMemory(raw, out _, out _));
    }

}
