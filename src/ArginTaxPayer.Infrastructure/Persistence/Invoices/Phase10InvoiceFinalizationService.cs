using System.Data;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Finalizes only an internally validated, approved immutable Sandbox revision.
/// Does not certify fiscal rules, allocate a serial, or send an invoice.
/// </summary>
public sealed class Phase10InvoiceFinalizationService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<Phase10InternalValidationResult> FinalizeAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId, long expectedRevision,
        CancellationToken ct = default)
    {
        if (taxpayerId == Guid.Empty || invoiceId == Guid.Empty || expectedRevision < 1)
            return new(InvoiceApiDecision.Invalid, "invalid_finalization_request");
        var principal = await scope.ResolveAsync(bearer, ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated, "unauthenticated");
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden, "forbidden");

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var owner = await scope.ScopedCommandQuery(principal.ActorId, principal.Level1)
                    .Where(t => t.Id == taxpayerId).Select(t => t.CustomerUserId)
                    .SingleOrDefaultAsync(ct);
                if (owner is null || owner == Guid.Empty)
                    return new Phase10InternalValidationResult(InvoiceApiDecision.NotFound, "invoice_not_found");
                if (!await db.CustomerTaxpayerMemberships.AsNoTracking().AnyAsync(m =>
                        m.CustomerUserId == owner.Value && m.TaxpayerId == taxpayerId &&
                        m.UnlinkedAtUtc == null, ct))
                    return new Phase10InternalValidationResult(InvoiceApiDecision.Forbidden, "membership_inactive");

                var invoice = await db.InvoiceDocuments.AsNoTracking()
                    .Where(i => i.Id == invoiceId && i.TaxpayerId == taxpayerId &&
                        i.CustomerUserId == owner.Value)
                    .Select(i => new { i.State, i.Revision, i.SnapshotJson, i.SnapshotSha256 })
                    .SingleOrDefaultAsync(ct);
                if (invoice is null)
                    return new Phase10InternalValidationResult(InvoiceApiDecision.NotFound, "invoice_not_found");
                if (invoice.Revision != expectedRevision)
                    return new Phase10InternalValidationResult(InvoiceApiDecision.Conflict, "stale_invoice_revision");
                if (invoice.State == InvoiceLifecycleState.Finalized)
                    return new Phase10InternalValidationResult(InvoiceApiDecision.Success, "already_finalized");
                if (invoice.State != InvoiceLifecycleState.Validated)
                    return new Phase10InternalValidationResult(InvoiceApiDecision.Invalid, "invoice_not_validated");
                if (!await db.InvoiceReviews.AsNoTracking().AnyAsync(r =>
                        r.InvoiceId == invoiceId && r.CustomerUserId == owner.Value &&
                        r.TaxpayerId == taxpayerId && r.State == InvoiceReviewState.Approved, ct))
                    return new Phase10InternalValidationResult(InvoiceApiDecision.Invalid, "review_not_approved");
                var captured = await db.InvoiceDocumentRevisions.AsNoTracking()
                    .AnyAsync(r => r.InvoiceId == invoiceId && r.Revision == expectedRevision &&
                        r.CustomerUserId == owner.Value && r.TaxpayerId == taxpayerId &&
                        r.SnapshotSha256 == invoice.SnapshotSha256 &&
                        r.SnapshotJson == invoice.SnapshotJson, ct);
                if (!captured || !Phase10InternalInvoiceValidationService.ValidSandboxSnapshot(
                        invoice.SnapshotJson, invoice.SnapshotSha256, invoiceId))
                    return new Phase10InternalValidationResult(InvoiceApiDecision.Invalid, "snapshot_invalid");

                var changed = await db.InvoiceDocuments.Where(i =>
                        i.Id == invoiceId && i.TaxpayerId == taxpayerId &&
                        i.CustomerUserId == owner.Value && i.Revision == expectedRevision &&
                        i.State == InvoiceLifecycleState.Validated &&
                        i.SnapshotSha256 == invoice.SnapshotSha256)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(i => i.State, InvoiceLifecycleState.Finalized)
                        .SetProperty(i => i.UpdatedAtUtc, DateTime.UtcNow), ct);
                if (changed != 1)
                    return new Phase10InternalValidationResult(InvoiceApiDecision.Conflict, "concurrent_invoice_change");

                await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO sandbox_invoice_lifecycle_events
 (id, customer_user_id, taxpayer_id, invoice_id, invoice_revision,
  from_state, to_state, snapshot_sha256, actor_user_id, created_at_utc)
VALUES ({Guid.NewGuid()}, {owner.Value}, {taxpayerId}, {invoiceId}, {expectedRevision},
 {(int)InvoiceLifecycleState.Validated}, {(int)InvoiceLifecycleState.Finalized},
 {invoice.SnapshotSha256}, {principal.ActorId}, {DateTime.UtcNow})
""", ct);
                await tx.CommitAsync(ct);
                return new Phase10InternalValidationResult(InvoiceApiDecision.Success, "internal_finalization_completed");
            });
        }
        catch (PostgresException error) when(error.SqlState is
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return new(InvoiceApiDecision.Conflict, "concurrent_finalization");
        }
    }
}
