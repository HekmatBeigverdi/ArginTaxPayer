using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoiceConcurrencyIdempotencyTests
{
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Taxpayer = Guid.NewGuid();
    private const string Reference = "A123456789012345678901";

    private static InvoiceOperationCommand Command(
        string key = "request-1", string fingerprint = "",
        InvoiceSubject subject = InvoiceSubject.Correction,
        string reference = Reference, long expectedRevision = 3,
        Guid? customer = null, Guid? taxpayer = null,
        Guid? invoiceId = null) =>
        new(customer ?? Customer, taxpayer ?? Taxpayer, invoiceId ?? Guid.Parse(
            "11111111-1111-1111-1111-111111111111"), expectedRevision,
            key, fingerprint == "" ? new string('a', 64) : fingerprint,
            subject, subject == InvoiceSubject.Original ? null : reference);

    [Fact]
    public void Reservation_advances_registry_revision_without_mutating_source()
    {
        var original = new InvoiceOperationRegistry();
        var outcome = original.Propose(Command(), 3, 0);
        Assert.Equal(InvoiceOperationDecision.Reserved, outcome.Decision);
        Assert.Equal(0, original.Revision);
        Assert.Empty(original.Reservations);
        Assert.Equal(1, outcome.Registry.Revision);
        Assert.Single(outcome.Registry.Reservations);
        Assert.False(outcome.IsSubmissionAuthorized);
    }

    [Fact]
    public void Identical_request_replayed_after_success_does_not_duplicate()
    {
        var registry = new InvoiceOperationRegistry()
            .Propose(Command(), 3, 0).Registry;
        var replay = registry.Propose(Command(), currentInvoiceRevision: 4,
            expectedRegistryRevision: 0);
        Assert.Equal(InvoiceOperationDecision.IdenticalReplay, replay.Decision);
        Assert.Same(registry, replay.Registry);
        Assert.Single(replay.Registry.Reservations);
    }

    [Fact]
    public void Same_key_different_fingerprint_or_invoice_is_a_conflict()
    {
        var registry = new InvoiceOperationRegistry().Propose(Command(), 3, 0).Registry;
        Assert.Equal(InvoiceOperationDecision.IdempotencyKeyConflict,
            registry.Propose(Command(fingerprint: new string('b', 64)), 3, 1).Decision);
        Assert.Equal(InvoiceOperationDecision.IdempotencyKeyConflict,
            registry.Propose(Command(invoiceId: Guid.NewGuid()), 3, 1).Decision);
        Assert.Equal(InvoiceOperationDecision.IdempotencyKeyConflict,
            registry.Propose(Command(expectedRevision: 4), 4, 1).Decision);
    }

    [Fact]
    public void Different_key_cannot_claim_same_correction_or_return_reference()
    {
        var registry = new InvoiceOperationRegistry().Propose(Command(), 3, 0).Registry;
        Assert.Equal(InvoiceOperationDecision.ReferenceConflict,
            registry.Propose(Command(key: "request-2",
                subject: InvoiceSubject.SalesReturn), 3, 1).Decision);
        Assert.Equal(InvoiceOperationDecision.ReferenceConflict,
            registry.Propose(Command(key: "request-3",
                subject: InvoiceSubject.Correction), 3, 1).Decision);
    }

    [Fact]
    public void Duplicate_cancellation_claim_is_rejected_but_has_independent_slot()
    {
        var registry = new InvoiceOperationRegistry().Propose(Command(), 3, 0).Registry;
        var cancel = registry.Propose(
            Command(key: "cancel-1", subject: InvoiceSubject.Cancellation), 3, 1);
        Assert.Equal(InvoiceOperationDecision.Reserved, cancel.Decision);
        Assert.Equal(InvoiceOperationDecision.ReferenceConflict,
            cancel.Registry.Propose(Command(key: "cancel-2",
                subject: InvoiceSubject.Cancellation), 3, 2).Decision);
    }

    [Fact]
    public void Stale_invoice_and_stale_registry_revision_are_distinct()
    {
        var registry = new InvoiceOperationRegistry();
        Assert.Equal(InvoiceOperationDecision.StaleInvoiceRevision,
            registry.Propose(Command(expectedRevision: 2), 3, 0).Decision);
        Assert.Equal(InvoiceOperationDecision.StaleRegistryRevision,
            registry.Propose(Command(), 3, 4).Decision);
        Assert.Empty(registry.Reservations);
    }

    [Fact]
    public void Same_keys_are_separate_across_tenant_scopes()
    {
        var registry = new InvoiceOperationRegistry().Propose(Command(), 3, 0).Registry;
        var differentCustomer = registry.Propose(
            Command(customer: Guid.NewGuid()), 3, 1);
        Assert.Equal(InvoiceOperationDecision.Reserved, differentCustomer.Decision);
        var differentTaxpayer = differentCustomer.Registry.Propose(
            Command(taxpayer: Guid.NewGuid()), 3, 2);
        Assert.Equal(InvoiceOperationDecision.Reserved, differentTaxpayer.Decision);
    }

    [Fact]
    public void Invalid_keys_fingerprints_and_reference_subjects_fail()
    {
        Assert.Throws<ArgumentException>(() => Command(key: " "));
        Assert.Throws<ArgumentException>(() => Command(fingerprint: "short"));
        Assert.Throws<ArgumentException>(() => Command(
            subject: InvoiceSubject.SalesReturn, reference: "INVALID"));
    }

    [Fact]
    public void External_registration_list_cannot_change_snapshot()
    {
        var items = new List<InvoiceOperationReservation> {
            new(Command())
        };
        var registry = new InvoiceOperationRegistry(1, items);
        items.Clear();
        Assert.Single(registry.Reservations);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<InvoiceOperationReservation>)registry.Reservations).Clear());
    }

    [Fact]
    public void Competing_proposals_from_same_snapshot_do_not_prove_atomicity()
    {
        var original = new InvoiceOperationRegistry();
        var a = original.Propose(Command("first"), 3, 0);
        var b = original.Propose(Command("second"), 3, 0);
        Assert.Equal(InvoiceOperationDecision.Reserved, a.Decision);
        Assert.Equal(InvoiceOperationDecision.Reserved, b.Decision);
        Assert.Equal(1, a.Registry.Revision);
        Assert.Equal(1, b.Registry.Revision);
        // A real DB CAS commit must permit at most one revision=0 -> 1.
        Assert.Empty(original.Reservations);
    }
}
