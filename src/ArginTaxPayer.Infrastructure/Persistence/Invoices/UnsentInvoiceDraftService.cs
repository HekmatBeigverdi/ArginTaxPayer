using System.Data;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record UnsentInvoiceDraftResult(InvoiceApiDecision Decision,
    string Code, Guid? WorkspaceId = null, bool ExistingWorkspace = false);

/// <summary>Returns the existing unsent invoice and its workspace to editable draft state.</summary>
public sealed class UnsentInvoiceDraftService(ArginTaxPayerDbContext db,
    TaxpayerScopeService scope)
{
    public async Task<UnsentInvoiceDraftResult> CreateAsync(string? bearer,
        Guid taxpayerId, Guid invoiceId, CancellationToken ct = default)
    {
        if (taxpayerId == Guid.Empty || invoiceId == Guid.Empty)
            return new(InvoiceApiDecision.Invalid, "invalid_invoice");
        var actor = await scope.ResolveAsync(bearer, ct);
        if (actor.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated, "unauthenticated");
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden, "forbidden");
        var owner = await scope.ScopedQuery(actor.ActorId, actor.Level1)
            .Where(t => t.Id == taxpayerId).Select(t => t.CustomerUserId).SingleOrDefaultAsync(ct);
        if (owner is null || owner == Guid.Empty)
            return new(InvoiceApiDecision.NotFound, "invoice_not_found");
        if (!await db.CustomerTaxpayerMemberships.AsNoTracking().AnyAsync(m =>
                m.CustomerUserId == owner.Value && m.TaxpayerId == taxpayerId && m.UnlinkedAtUtc == null, ct))
            return new(InvoiceApiDecision.Forbidden, "membership_inactive");

        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync<UnsentInvoiceDraftResult>(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                // Serialize against saves, promotion, and dispatch creation using the existing IDs.
                var connection = db.Database.GetDbConnection();
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
                    command.CommandText = "SELECT id FROM invoice_documents WHERE id=@id AND customer_user_id=@owner AND taxpayer_id=@taxpayer FOR UPDATE";
                    foreach (var (name, value) in new[] { ("id", invoiceId), ("owner", owner.Value), ("taxpayer", taxpayerId) })
                    { var p = command.CreateParameter(); p.ParameterName = name; p.Value = value; command.Parameters.Add(p); }
                    if (await command.ExecuteScalarAsync(ct) is null)
                        return new(InvoiceApiDecision.NotFound, "invoice_not_found");
                }
                var invoice = await db.InvoiceDocuments.AsNoTracking().SingleOrDefaultAsync(i =>
                    i.Id == invoiceId && i.CustomerUserId == owner.Value && i.TaxpayerId == taxpayerId, ct);
                if (invoice is null) return new(InvoiceApiDecision.NotFound, "invoice_not_found");
                if (invoice.Subject != InvoiceSubject.Original ||
                    invoice.State is not (InvoiceLifecycleState.Draft or InvoiceLifecycleState.Validated or InvoiceLifecycleState.Finalized))
                    return new(InvoiceApiDecision.Conflict, "invoice_not_eligible");
                if (await HasDispatchOperationAsync(owner.Value, taxpayerId, invoiceId, ct))
                    return new(InvoiceApiDecision.Conflict, "invoice_has_dispatch_operation");

                var source = await db.InvoiceEntryWorkspaces.SingleOrDefaultAsync(w =>
                    w.Id == invoiceId && w.CustomerUserId == owner.Value && w.TaxpayerId == taxpayerId, ct);
                if (source is null) return new(InvoiceApiDecision.Conflict, "source_workspace_missing");
                var returnKey = $"unsent-return:{invoiceId:N}";
                SaveInvoiceEntryRequest input;
                try { input = JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(source.DraftJson) ?? throw new JsonException(); }
                catch (JsonException) { return new(InvoiceApiDecision.Invalid, "source_workspace_invalid"); }
                if (invoice.State == InvoiceLifecycleState.Draft && input.BulkImportKey == returnKey)
                {
                    await tx.CommitAsync(ct);
                    return new(InvoiceApiDecision.Success, "draft_ready", invoiceId, true);
                }
                var returnedJson = JsonSerializer.Serialize(input with {
                    Id = null, ExpectedRevision = 0, BulkImportKey = returnKey, BulkFingerprint = null,
                    InternalReviewState = "Draft", InternalReviewReason = null, SubmittedByUserId = null,
                    ReviewedByUserId = null, ReviewedAtUtc = null
                });
                var now = DateTime.UtcNow;
                var nextWorkspaceRevision = source.Revision + 1;
                var workspaceChanged = await db.InvoiceEntryWorkspaces.Where(w => w.Id == invoiceId &&
                    w.CustomerUserId == owner.Value && w.TaxpayerId == taxpayerId && w.Revision == source.Revision)
                    .ExecuteUpdateAsync(set => set.SetProperty(w => w.Revision, nextWorkspaceRevision)
                        .SetProperty(w => w.DraftJson, returnedJson).SetProperty(w => w.UpdatedAtUtc, now), ct);
                if (workspaceChanged != 1) return new(InvoiceApiDecision.Conflict, "concurrent_workspace_change");
                db.InvoiceEntryWorkspaceRevisions.Add(new InvoiceEntryWorkspaceRevision {
                    WorkspaceId = invoiceId, CustomerUserId = owner.Value, TaxpayerId = taxpayerId,
                    Revision = nextWorkspaceRevision, ActorUserId = actor.ActorId,
                    Action = "save", Reason = "بازگشت صورتحساب ارسال‌نشده به پیش‌نویس",
                    SnapshotJson = returnedJson, OccurredAtUtc = now
                });
                var nextInvoiceRevision = invoice.Revision + 1;
                var documentChanged = await db.InvoiceDocuments.Where(i => i.Id == invoiceId &&
                    i.CustomerUserId == owner.Value && i.TaxpayerId == taxpayerId &&
                    i.Revision == invoice.Revision && i.State == invoice.State)
                    .ExecuteUpdateAsync(set => set.SetProperty(i => i.Revision, nextInvoiceRevision)
                        .SetProperty(i => i.State, InvoiceLifecycleState.Draft).SetProperty(i => i.UpdatedAtUtc, now), ct);
                if (documentChanged != 1) return new(InvoiceApiDecision.Conflict, "concurrent_invoice_change");
                db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision {
                    InvoiceId = invoiceId, CustomerUserId = owner.Value, TaxpayerId = taxpayerId,
                    Revision = nextInvoiceRevision, SnapshotJson = invoice.SnapshotJson,
                    SnapshotSha256 = invoice.SnapshotSha256, CapturedAtUtc = now
                });
                if (invoice.State != InvoiceLifecycleState.Draft)
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO sandbox_invoice_lifecycle_events
 (id, customer_user_id, taxpayer_id, invoice_id, invoice_revision, from_state, to_state,
  snapshot_sha256, actor_user_id, created_at_utc)
VALUES ({Guid.NewGuid()}, {owner.Value}, {taxpayerId}, {invoiceId}, {nextInvoiceRevision},
 {(int)invoice.State}, {(int)InvoiceLifecycleState.Draft}, {invoice.SnapshotSha256}, {actor.ActorId}, {now})
""", ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(InvoiceApiDecision.Success, "draft_ready", invoiceId, true);
            });
        }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        { db.ChangeTracker.Clear(); return new(InvoiceApiDecision.Conflict, "concurrent_invoice_change"); }
    }

    private async Task<bool> HasDispatchOperationAsync(Guid owner, Guid taxpayerId, Guid invoiceId, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM invoice_dispatch_operations WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND invoice_id=@invoice)";
            foreach (var (name, value) in new[] { ("owner", owner), ("taxpayer", taxpayerId), ("invoice", invoiceId) })
            { var p = command.CreateParameter(); p.ParameterName = name; p.Value = value; command.Parameters.Add(p); }
            return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
        }
        finally { if (openedHere) await connection.CloseAsync(); }
    }
}
