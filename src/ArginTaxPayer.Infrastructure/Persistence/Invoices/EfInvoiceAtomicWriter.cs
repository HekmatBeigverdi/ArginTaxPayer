using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum InvoiceWriteStatus
{
    Applied, IdenticalReplay, KeyConflict, ReferenceConflict,
    StaleRevision, StaleRegistryRevision, NotFoundOrWrongScope
}
public sealed record InvoiceWriteResult(InvoiceWriteStatus Status, long? Revision)
{
    public bool IsFiscalSubmissionAuthorized => false;
}

/// <summary>
/// Transactional PostgreSQL writer. The caller supplies a trusted, authenticated
/// scope and a canonical snapshot. This service deliberately never sends a fiscal
/// invoice or assumes a client-provided scope grants authorization.
/// </summary>
public sealed class EfInvoiceAtomicWriter(ArginTaxPayerDbContext db)
{
    /// <summary>Insert a new initial draft; caller must pre-authorize tenant and source.</summary>
    public async Task<bool> CreateDraftAsync(Guid invoiceId, Guid customerId, Guid taxpayerId,
        DateTime issueDateUtc, string canonicalSnapshotJson, DateTime atUtc,
        CancellationToken ct = default)
    {
        if (invoiceId == Guid.Empty || customerId == Guid.Empty || taxpayerId == Guid.Empty ||
            issueDateUtc.Kind != DateTimeKind.Utc || atUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid draft ownership or timestamps.");
        var fingerprint = Hash(canonicalSnapshotJson);
        try
        {
            db.InvoiceDocuments.Add(new StoredInvoice
            {
                Id = invoiceId, CustomerUserId = customerId, TaxpayerId = taxpayerId,
                Revision = 1, State = InvoiceLifecycleState.Draft, Subject = InvoiceSubject.Original,
                IssueDateUtc = issueDateUtc, SnapshotJson = canonicalSnapshotJson,
                SnapshotSha256 = fingerprint, UpdatedAtUtc = atUtc
            });
            db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision
            {
                InvoiceId = invoiceId, CustomerUserId = customerId,
                TaxpayerId = taxpayerId, Revision = 1,
                SnapshotJson = canonicalSnapshotJson, SnapshotSha256 = fingerprint,
                CapturedAtUtc = atUtc
            });
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (IsUnique(e))
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    /// <summary>
    /// All three actions in one transaction: CAS invoice revision, append
    /// idempotency outcome, and claim exclusive reference slot. Failed uniqueness
    /// rolls back invoice mutation. No process-global mutex is used.
    /// </summary>
    public async Task<InvoiceWriteResult> ApplyAsync(InvoiceOperationCommand command,
        long expectedRegistryRevision, string nextSnapshotJson, InvoiceLifecycleState nextState, DateTime issueDateUtc,
        DateTime atUtc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (expectedRegistryRevision < 0 || atUtc.Kind != DateTimeKind.Utc || issueDateUtc.Kind != DateTimeKind.Utc ||
            !Enum.IsDefined(nextState))
            throw new ArgumentException("Invalid UTC write or internal state.");

        var snapshotHash = Hash(nextSnapshotJson);
        // A canonical request fingerprint must be independently produced and
        // verified by the trusted application service; hash here is of the
        // resulting snapshot, not proof of full request semantics.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var prior = await db.InvoiceOperations.AsNoTracking().FirstOrDefaultAsync(
                x => x.CustomerUserId == command.CustomerUserId &&
                     x.TaxpayerId == command.TaxpayerId &&
                     x.IdempotencyKey == command.IdempotencyKey, ct);
            if (prior is not null)
            {
                await tx.RollbackAsync(ct);
                return Prior(prior, command);
            }

            // Initialize cursor and compare-and-swap inside this same transaction.
            // Distinct invoices in one tenant scope cannot both commit from a
            // shared stale registry view. PostgreSQL row update serializes writers.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO invoice_reservation_cursors(customer_user_id,taxpayer_id,revision)
                    VALUES ({command.CustomerUserId},{command.TaxpayerId},0)
                    ON CONFLICT (customer_user_id,taxpayer_id) DO NOTHING", ct);
            var cursorChanged = await db.InvoiceReservationCursors
                .Where(x => x.CustomerUserId == command.CustomerUserId &&
                    x.TaxpayerId == command.TaxpayerId &&
                    x.Revision == expectedRegistryRevision)
                .ExecuteUpdateAsync(x => x.SetProperty(e => e.Revision,
                    e => e.Revision + 1), ct);
            if (cursorChanged != 1)
            {
                await tx.RollbackAsync(ct);
                return new(InvoiceWriteStatus.StaleRegistryRevision, null);
            }

            var changed = await db.InvoiceDocuments
                .Where(x => x.Id == command.InvoiceId &&
                    x.CustomerUserId == command.CustomerUserId &&
                    x.TaxpayerId == command.TaxpayerId &&
                    x.Revision == command.ExpectedInvoiceRevision)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(e => e.Revision, e => e.Revision + 1)
                    .SetProperty(e => e.State, nextState)
                    .SetProperty(e => e.Subject, command.Subject)
                    .SetProperty(e => e.IssueDateUtc, issueDateUtc)
                    .SetProperty(e => e.SnapshotJson, nextSnapshotJson)
                    .SetProperty(e => e.SnapshotSha256, snapshotHash)
                    .SetProperty(e => e.UpdatedAtUtc, atUtc), ct);
            if (changed != 1)
            {
                await tx.RollbackAsync(ct);
                var exists = await db.InvoiceDocuments.AsNoTracking().AnyAsync(x =>
                    x.Id == command.InvoiceId &&
                    x.CustomerUserId == command.CustomerUserId &&
                    x.TaxpayerId == command.TaxpayerId, ct);
                return new(exists ? InvoiceWriteStatus.StaleRevision :
                    InvoiceWriteStatus.NotFoundOrWrongScope, null);
            }

            var referenceSlot = command.Subject switch
            {
                InvoiceSubject.Correction or InvoiceSubject.SalesReturn => 1,
                InvoiceSubject.Cancellation => 2,
                _ => 0
            };
            db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision
            {
                InvoiceId = command.InvoiceId,
                CustomerUserId = command.CustomerUserId,
                TaxpayerId = command.TaxpayerId,
                Revision = checked(command.ExpectedInvoiceRevision + 1),
                SnapshotJson = nextSnapshotJson, SnapshotSha256 = snapshotHash,
                CapturedAtUtc = atUtc
            });
            db.InvoiceOperations.Add(new StoredInvoiceOperation
            {
                Id = Guid.NewGuid(), CustomerUserId = command.CustomerUserId,
                TaxpayerId = command.TaxpayerId, InvoiceId = command.InvoiceId,
                IdempotencyKey = command.IdempotencyKey,
                FingerprintSha256 = command.RequestFingerprintSha256,
                ExpectedRevision = command.ExpectedInvoiceRevision,
                Subject = command.Subject, ReferenceTaxId = command.ReferenceTaxId,
                ReferenceSlot = referenceSlot, IsClaimActive = referenceSlot != 0,
                OutcomeJson = JsonSerializer.Serialize(new
                {
                    status = "applied", revision = command.ExpectedInvoiceRevision + 1
                }),
                CreatedAtUtc = atUtc
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(InvoiceWriteStatus.Applied, command.ExpectedInvoiceRevision + 1);
        }
        catch (DbUpdateException e) when (IsUnique(e))
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var prior = await db.InvoiceOperations.AsNoTracking().FirstOrDefaultAsync(x =>
                x.CustomerUserId == command.CustomerUserId &&
                x.TaxpayerId == command.TaxpayerId &&
                x.IdempotencyKey == command.IdempotencyKey, ct);
            return prior is null
                ? new(InvoiceWriteStatus.ReferenceConflict, null)
                : Prior(prior, command);
        }
    }

    private static InvoiceWriteResult Prior(
        StoredInvoiceOperation previous, InvoiceOperationCommand command) =>
        previous.InvoiceId == command.InvoiceId &&
        previous.ExpectedRevision == command.ExpectedInvoiceRevision &&
        previous.FingerprintSha256 == command.RequestFingerprintSha256 &&
        previous.Subject == command.Subject &&
        previous.ReferenceTaxId == command.ReferenceTaxId
            ? new(InvoiceWriteStatus.IdenticalReplay, previous.ExpectedRevision + 1)
            : new(InvoiceWriteStatus.KeyConflict, null);

    private static bool IsUnique(DbUpdateException exception) =>
        exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string Hash(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Canonical invoice JSON must be nonempty.");
        using var document = JsonDocument.Parse(json);
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
