namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Domain contract for CAS and scoped idempotent reference reservation.
/// This is NOT a distributed lock or a database uniqueness constraint.
/// Persistence Step18 must execute comparisons and inserts atomically.
/// </summary>
public sealed record InvoiceOperationCommand
{
    public Guid CustomerUserId { get; }
    public Guid TaxpayerId { get; }
    public Guid InvoiceId { get; }
    public long ExpectedInvoiceRevision { get; }
    public string IdempotencyKey { get; }
    public string RequestFingerprintSha256 { get; }
    public InvoiceSubject Subject { get; }
    public string? ReferenceTaxId { get; }

    public InvoiceOperationCommand(Guid customerUserId, Guid taxpayerId,
        Guid invoiceId, long expectedInvoiceRevision, string idempotencyKey,
        string requestFingerprintSha256, InvoiceSubject subject,
        string? referenceTaxId = null)
    {
        if (customerUserId == Guid.Empty || taxpayerId == Guid.Empty ||
            invoiceId == Guid.Empty || expectedInvoiceRevision < 1 ||
            string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length > 128 ||
            idempotencyKey.Any(char.IsControl) ||
            !InvoiceValidationReceipt.IsSha256(requestFingerprintSha256) ||
            !Enum.IsDefined(subject) ||
            (subject == InvoiceSubject.Original && referenceTaxId is not null) ||
            (subject != InvoiceSubject.Original &&
             (referenceTaxId is not { Length: 22 } ||
              !referenceTaxId.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z'))))
            throw new ArgumentException("Invalid invoice operation identity or request fingerprint.");
        CustomerUserId = customerUserId;
        TaxpayerId = taxpayerId;
        InvoiceId = invoiceId;
        ExpectedInvoiceRevision = expectedInvoiceRevision;
        IdempotencyKey = idempotencyKey;
        RequestFingerprintSha256 = requestFingerprintSha256.ToLowerInvariant();
        Subject = subject;
        ReferenceTaxId = referenceTaxId;
    }
}

public enum InvoiceOperationDecision
{
    Reserved = 1,
    IdenticalReplay = 2,
    IdempotencyKeyConflict = 3,
    ReferenceConflict = 4,
    StaleInvoiceRevision = 5,
    StaleRegistryRevision = 6
}

/// <summary>
/// An immutable reservation. A replay must match ALL request identifiers,
/// revision, payload fingerprint, subject and fiscal predecessor.
/// </summary>
public sealed record InvoiceOperationReservation(InvoiceOperationCommand Command);

public sealed record InvoiceOperationOutcome(
    InvoiceOperationDecision Decision,
    InvoiceOperationRegistry Registry)
{
    public bool IsSubmissionAuthorized => false;
}

/// <summary>
/// In-memory pure transition proposal. Never use Registry instances as a
/// concurrency barrier across requests or hosts. Storage must enforce CAS,
/// unique (customer,taxpayer,idempotencyKey), and scoped reference claims.
/// </summary>
public sealed class InvoiceOperationRegistry
{
    private readonly IReadOnlyList<InvoiceOperationReservation> _reservations;
    public long Revision { get; }
    public IReadOnlyList<InvoiceOperationReservation> Reservations => _reservations;

    public InvoiceOperationRegistry(long revision = 0,
        IEnumerable<InvoiceOperationReservation>? reservations = null)
    {
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        var entries = (reservations ?? Array.Empty<InvoiceOperationReservation>()).ToArray();
        if (entries.Any(x => x is null || x.Command is null) ||
            entries.GroupBy(x => (x.Command.CustomerUserId, x.Command.TaxpayerId,
                x.Command.IdempotencyKey), x => x).Any(x => x.Count() != 1))
            throw new ArgumentException("Duplicate or invalid idempotency reservations.");
        Revision = revision;
        _reservations = Array.AsReadOnly(entries);
    }

    public InvoiceOperationOutcome Propose(InvoiceOperationCommand command,
        long currentInvoiceRevision, long expectedRegistryRevision)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (currentInvoiceRevision < 1 || expectedRegistryRevision < 0)
            throw new ArgumentOutOfRangeException("Invalid CAS revision.");

        // Replay must be recognized before comparing invoice revision: a
        // successful prior request already advanced the aggregate revision.
        var previous = _reservations.FirstOrDefault(x =>
            x.Command.CustomerUserId == command.CustomerUserId &&
            x.Command.TaxpayerId == command.TaxpayerId &&
            x.Command.IdempotencyKey == command.IdempotencyKey);

        if (previous is not null)
        {
            if (previous.Command == command)
                return new(InvoiceOperationDecision.IdenticalReplay, this);
            return new(InvoiceOperationDecision.IdempotencyKeyConflict, this);
        }
        if (expectedRegistryRevision != Revision)
            return new(InvoiceOperationDecision.StaleRegistryRevision, this);
        if (command.ExpectedInvoiceRevision != currentInvoiceRevision)
            return new(InvoiceOperationDecision.StaleInvoiceRevision, this);

        // A correction and a sales return claim the same exclusive reference
        // slot until the authoritative status invalidates a prior claim.
        // Cancellation has its own exclusive slot (at most one per source).
        var clashes = _reservations.Any(x =>
            x.Command.CustomerUserId == command.CustomerUserId &&
            x.Command.TaxpayerId == command.TaxpayerId &&
            x.Command.ReferenceTaxId == command.ReferenceTaxId &&
            command.ReferenceTaxId is not null &&
            ((command.Subject == InvoiceSubject.Cancellation &&
              x.Command.Subject == InvoiceSubject.Cancellation) ||
             (command.Subject is InvoiceSubject.Correction or InvoiceSubject.SalesReturn &&
              x.Command.Subject is InvoiceSubject.Correction or InvoiceSubject.SalesReturn)));
        if (clashes)
            return new(InvoiceOperationDecision.ReferenceConflict, this);

        var copy = _reservations.Concat(new[]
        {
            new InvoiceOperationReservation(command)
        }).ToArray();
        return new(InvoiceOperationDecision.Reserved,
            new InvoiceOperationRegistry(checked(Revision + 1), copy));
    }
}
