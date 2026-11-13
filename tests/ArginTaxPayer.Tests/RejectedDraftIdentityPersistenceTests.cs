using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public class RejectedDraftIdentityPersistenceTests
{
    [Fact]
    public void Repeated_form_saves_preserve_retry_identity_and_allow_memory_changes()
    {
        var original = new SaveInvoiceEntryRequest(Guid.NewGuid(), Guid.NewGuid(), 1,
            0, DateTime.UtcNow, null, 1, 1, [],
            BulkImportKey: "sandbox-correction:operation:retry:1");
        var creation = JsonSerializer.Serialize(original);
        var stored = creation;
        for (var revision = 1; revision <= 5; revision++)
        {
            var memory = Guid.NewGuid();
            var form = original with { ExpectedRevision = revision,
                BulkImportKey = null, SandboxFiscalMemoryId = memory };
            var saved = InvoiceEntryWorkspaceService.PreserveWorkspaceIdentity(form, stored, creation);
            Assert.Equal(original.BulkImportKey, saved.BulkImportKey);
            Assert.Equal(memory, saved.SandboxFiscalMemoryId);
            Assert.Equal(revision, saved.ExpectedRevision);
            stored = JsonSerializer.Serialize(saved);
        }
    }

    [Fact]
    public void Lost_identity_is_recovered_from_creation_history_and_cannot_be_replaced_by_client()
    {
        var original = new SaveInvoiceEntryRequest(Guid.NewGuid(), Guid.NewGuid(), 1,
            0, DateTime.UtcNow, null, 1, 1, [], BulkImportKey: "sandbox-correction:operation:retry:2");
        var damaged = JsonSerializer.Serialize(original with { BulkImportKey = null });
        var result = InvoiceEntryWorkspaceService.PreserveWorkspaceIdentity(
            original with { BulkImportKey = "forged-key" }, damaged, JsonSerializer.Serialize(original));
        Assert.Equal(original.BulkImportKey, result.BulkImportKey);
    }
}
