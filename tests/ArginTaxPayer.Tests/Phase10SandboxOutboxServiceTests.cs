using ArginTaxPayer.Application.Abstractions.Messaging;
using ArginTaxPayer.Application.Abstractions.Persistence;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10SandboxOutboxServiceTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Taxpayer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Memory = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Invoice = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Operation = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static (InvoiceDispatchAdmission Admission, Phase10SandboxInvoicePacket Packet) Valid()
    {
        var jweHash = new string('a', 64);
        var admission = InvoiceDispatchAdmission.Create(Operation, Customer, Taxpayer,
            Memory, Invoice, 9, jweHash, "sandbox-op-9");
        return (admission, new(
            Invoice, Customer, Taxpayer, Memory, 9, new string('b', 64),
            "ABC1230644600000000017", Guid.Parse("66666666-6666-6666-6666-666666666666"),
            new string('c', 64), "sandbox-key", "jws", "encrypted-jwe", new string('d', 64), jweHash));
    }

    [Fact]
    public async Task Outbox_is_enqueued_inside_unit_of_work_before_prepare_returns()
    {
        var (admission, packet) = Valid();
        var writer = new RecordingOutboxWriter();
        var unit = new RecordingUnitOfWork(writer);
        var service = new Phase10SandboxOutboxService(writer, unit);

        var result = await service.PrepareAsync(admission, packet);

        Assert.Equal(Phase10SandboxOutboxPreparationStatus.Prepared, result.Status);
        Assert.True(unit.TransactionCompleted);
        Assert.True(writer.WasEnqueued);
        Assert.Equal(Operation, writer.MessageId);
        Assert.Equal("invoice.sandbox.dispatch.requested", writer.EventType);
        Assert.Contains("\"environment\":\"Sandbox\"", writer.PayloadJson);
        Assert.Contains("\"automaticRetry\":false", writer.PayloadJson);
        Assert.DoesNotContain("PRIVATE KEY", writer.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Conflicting_revision_or_memory_is_rejected_without_outbox_side_effect()
    {
        var (admission, packet) = Valid();
        var writer = new RecordingOutboxWriter();
        var unit = new RecordingUnitOfWork(writer);
        var service = new Phase10SandboxOutboxService(writer, unit);

        var result = await service.PrepareAsync(admission,
            packet with { InvoiceRevision = packet.InvoiceRevision + 1 });

        Assert.Equal(Phase10SandboxOutboxPreparationStatus.Rejected, result.Status);
        Assert.Equal("packet_identity_mismatch", result.Code);
        Assert.False(writer.WasEnqueued);
        Assert.False(unit.TransactionCompleted);
    }

    private sealed class RecordingOutboxWriter : IOutboxWriter
    {
        public bool WasEnqueued { get; private set; }
        public Guid MessageId { get; private set; }
        public string EventType { get; private set; } = "";
        public string PayloadJson { get; private set; } = "";

        public Task EnqueueAsync(Guid messageId, string eventType, string payloadJson,
            string deduplicationKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasEnqueued = true;
            MessageId = messageId;
            EventType = eventType;
            PayloadJson = payloadJson;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork(RecordingOutboxWriter writer) : IUnitOfWork
    {
        public bool TransactionCompleted { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            await operation(cancellationToken);
            Assert.True(writer.WasEnqueued);
            TransactionCompleted = true;
        }
    }
}
