using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase10InternalValidationResult(InvoiceApiDecision Decision, string Code);

/// <summary>
/// Validates the existing immutable internal Sandbox snapshot before the
/// separate review and fiscal-rule approval process. No sending or signing.
/// </summary>
public sealed class Phase10InternalInvoiceValidationService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<Phase10InternalValidationResult> ValidateAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId, long expectedRevision,
        CancellationToken ct = default)
    {
        if (taxpayerId == Guid.Empty || invoiceId == Guid.Empty || expectedRevision < 1)
            return new(InvoiceApiDecision.Invalid, "invalid_validation_request");
        var actor = await scope.ResolveAsync(bearer, ct);
        if (actor.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated, "unauthenticated");
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden, "forbidden");
        var owner = await scope.ScopedQuery(actor.ActorId, actor.Level1)
            .Where(t => t.Id == taxpayerId).Select(t => t.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if (owner is null || owner == Guid.Empty)
            return new(InvoiceApiDecision.NotFound, "invoice_not_found");
        var membership = await db.CustomerTaxpayerMemberships.AsNoTracking()
            .AnyAsync(m => m.CustomerUserId == owner.Value &&
                m.TaxpayerId == taxpayerId && m.UnlinkedAtUtc == null, ct);
        if (!membership)
            return new(InvoiceApiDecision.Forbidden, "membership_inactive");

        var invoice = await db.InvoiceDocuments.AsNoTracking()
            .Where(i => i.Id == invoiceId && i.CustomerUserId == owner.Value &&
                i.TaxpayerId == taxpayerId)
            .Select(i => new { i.Revision, i.State, i.SnapshotJson, i.SnapshotSha256 })
            .SingleOrDefaultAsync(ct);
        if (invoice is null)
            return new(InvoiceApiDecision.NotFound, "invoice_not_found");
        if (invoice.Revision != expectedRevision)
            return new(InvoiceApiDecision.Conflict, "stale_invoice_revision");
        if (invoice.State == InvoiceLifecycleState.Validated)
            return new(InvoiceApiDecision.Success, "already_internally_validated");
        if (invoice.State != InvoiceLifecycleState.Draft)
            return new(InvoiceApiDecision.Conflict, "invoice_not_draft");
        if (!ValidSandboxSnapshot(invoice.SnapshotJson, invoice.SnapshotSha256, invoiceId))
            return new(InvoiceApiDecision.Invalid, "sandbox_snapshot_validation_failed");

        var changed = await db.InvoiceDocuments
            .Where(i => i.Id == invoiceId && i.CustomerUserId == owner.Value &&
                i.TaxpayerId == taxpayerId && i.Revision == expectedRevision &&
                i.State == InvoiceLifecycleState.Draft &&
                i.SnapshotSha256 == invoice.SnapshotSha256)
            .ExecuteUpdateAsync(update => update
                .SetProperty(i => i.State, InvoiceLifecycleState.Validated)
                .SetProperty(i => i.UpdatedAtUtc, DateTime.UtcNow), ct);
        return changed == 1
            ? new(InvoiceApiDecision.Success, "internal_validation_completed")
            : new(InvoiceApiDecision.Conflict, "concurrent_invoice_change");
    }

    internal static bool ValidSandboxSnapshot(string raw, string expectedHash, Guid invoiceId)
    {
        if (!InvoiceValidationReceipt.IsSha256(expectedHash)) return false;
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("sourceWorkspaceId", out var source) ||
                !source.TryGetGuid(out var sourceId) || sourceId != invoiceId ||
                !root.TryGetProperty("sourceRevision", out var revision) ||
                !revision.TryGetInt64(out var value) || value < 1 ||
                !root.TryGetProperty("environment", out var environment) ||
                environment.GetString() != "Sandbox" ||
                !root.TryGetProperty("sandboxFiscalMemoryId", out var memory) ||
                !memory.TryGetGuid(out var memoryId) || memoryId == Guid.Empty ||
                !root.TryGetProperty("buyerPartyId", out var buyer) ||
                !buyer.TryGetGuid(out var buyerId) || buyerId == Guid.Empty ||
                !root.TryGetProperty("lines", out var lines) ||
                lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() == 0 ||
                !root.TryGetProperty("settlementMethod", out _) ||
                !root.TryGetProperty("financialSummary", out _))
                return false;
            return true;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }
}
