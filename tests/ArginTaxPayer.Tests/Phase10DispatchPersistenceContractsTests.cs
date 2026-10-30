using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10DispatchPersistenceContractsTests
{
    private static InvoiceDispatchAdmission New(string key = "submission-123") =>
        InvoiceDispatchAdmission.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 1, new string('A', 64), key);

    [Fact]
    public void Same_scoped_key_and_immutable_payload_is_replay()
    {
        var a = New();
        var b = InvoiceDispatchAdmission.Create(Guid.NewGuid(), a.CustomerUserId,
            a.TaxpayerId, a.FiscalMemoryId, a.InvoiceId, a.InvoiceRevision,
            a.PayloadSha256.ToUpperInvariant(), a.IdempotencyKey);
        Assert.Equal(InvoiceAdmissionReplayDecision.ReplaySameOperation, a.CompareReplay(b));
        Assert.Equal(new string('a', 64), a.PayloadSha256);
    }

    [Fact]
    public void Same_key_and_changed_payload_or_memory_is_conflict()
    {
        var a = New();
        var changed = InvoiceDispatchAdmission.Create(Guid.NewGuid(), a.CustomerUserId,
            a.TaxpayerId, a.FiscalMemoryId, a.InvoiceId, a.InvoiceRevision,
            new string('b', 64), a.IdempotencyKey);
        var otherMemory = InvoiceDispatchAdmission.Create(Guid.NewGuid(), a.CustomerUserId,
            a.TaxpayerId, Guid.NewGuid(), a.InvoiceId, a.InvoiceRevision,
            a.PayloadSha256, a.IdempotencyKey);
        Assert.Equal(InvoiceAdmissionReplayDecision.ConflictingPayload, a.CompareReplay(changed));
        Assert.Equal(InvoiceAdmissionReplayDecision.ConflictingPayload, a.CompareReplay(otherMemory));
    }

    [Fact]
    public void Different_tenant_or_key_is_not_a_replay_in_this_namespace()
    {
        var a = New();
        Assert.Equal(InvoiceAdmissionReplayDecision.DifferentKeyOrScope,
            a.CompareReplay(New()));
        var otherTenant = InvoiceDispatchAdmission.Create(Guid.NewGuid(),
            Guid.NewGuid(), a.TaxpayerId, a.FiscalMemoryId, a.InvoiceId,
            a.InvoiceRevision, a.PayloadSha256, a.IdempotencyKey);
        Assert.Equal(InvoiceAdmissionReplayDecision.DifferentKeyOrScope,
            a.CompareReplay(otherTenant));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("line\nbreak")]
    [InlineData("non ascii →")]
    public void Malformed_idempotency_keys_are_denied(string key)
    {
        Assert.Throws<ArgumentException>(() =>
            InvoiceDispatchAdmission.Create(Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
                new string('a', 64), key));
    }

    [Fact]
    public void Expired_worker_leases_do_not_clear_uncertain_external_outcomes()
    {
        Assert.True(InvoiceDispatchStoragePolicy.CanRedriveOutboxWithoutReconciliation(
            InvoiceDispatchState.Reserved));
        Assert.False(InvoiceDispatchStoragePolicy.CanRedriveOutboxWithoutReconciliation(
            InvoiceDispatchState.Sending));
        Assert.False(InvoiceDispatchStoragePolicy.CanRedriveOutboxWithoutReconciliation(
            InvoiceDispatchState.Indeterminate));
        Assert.True(InvoiceDispatchStoragePolicy.RequiresExternalResolution(
            InvoiceDispatchState.Sending));
        Assert.True(InvoiceDispatchStoragePolicy.RequiresExternalResolution(
            InvoiceDispatchState.Indeterminate));
        Assert.False(InvoiceDispatchStoragePolicy.EligibleForDirectNewSend(
            InvoiceDispatchState.Acknowledged));
        foreach (InvoiceDispatchState state in Enum.GetValues<InvoiceDispatchState>())
            Assert.False(InvoiceDispatchStoragePolicy.CanRemovePersistedAttempt(state));
    }
}
