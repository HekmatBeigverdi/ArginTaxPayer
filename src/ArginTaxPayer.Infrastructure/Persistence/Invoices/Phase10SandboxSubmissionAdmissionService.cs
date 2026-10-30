using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record SandboxSubmissionAdmissionRequest(
    Guid SandboxFiscalMemoryId,
    long InvoiceRevision,
    string ApprovedSnapshotSha256,
    DateTimeOffset ExpiresAtUtc,
    bool OperatorConfirmed,
    string Environment);

public sealed record SandboxSubmissionAdmissionView(
    bool IsAdmitted, string Environment, long InvoiceRevision,
    string SnapshotSha256, string[] BlockingCodes);

public sealed record SandboxSubmissionAdmissionServiceResult(
    InvoiceApiDecision Decision, SandboxSubmissionAdmissionView? Value = null);

/// <summary>
/// Authenticated owner-scoped admission boundary. It evaluates server-owned
/// invoice/review data and short-lived consent only; it never signs, persists a
/// packet, mutates an invoice, or performs network I/O.
/// </summary>
public sealed class Phase10SandboxSubmissionAdmissionService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<SandboxSubmissionAdmissionServiceResult> EvaluateAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId,
        SandboxSubmissionAdmissionRequest? request, CancellationToken ct = default)
    {
        if (taxpayerId == Guid.Empty || invoiceId == Guid.Empty || request is null)
            return new(InvoiceApiDecision.Invalid);

        var actor = await scope.ResolveAsync(bearer, ct);
        if (actor.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated);
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);

        var owner = await scope.ScopedQuery(actor.ActorId, actor.Level1)
            .Where(x => x.Id == taxpayerId)
            .Select(x => x.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if (owner is null || owner.Value == Guid.Empty)
            return new(InvoiceApiDecision.NotFound);

        var invoice = await db.InvoiceDocuments.AsNoTracking()
            .Where(x => x.Id == invoiceId && x.TaxpayerId == taxpayerId &&
                x.CustomerUserId == owner.Value)
            .Select(x => new { x.Id, x.CustomerUserId, x.TaxpayerId, x.Revision,
                x.State, x.SnapshotJson, x.SnapshotSha256 })
            .SingleOrDefaultAsync(ct);
        if (invoice is null)
            return new(InvoiceApiDecision.NotFound);

        var reviewState = await db.InvoiceReviews.AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId &&
                x.CustomerUserId == owner.Value && x.TaxpayerId == taxpayerId)
            .Select(x => (InvoiceReviewState?)x.State)
            .SingleOrDefaultAsync(ct) ?? InvoiceReviewState.Draft;

        var environment = "Unverified";
        var memoryId = Guid.Empty;
        try
        {
            using var document = JsonDocument.Parse(invoice.SnapshotJson);
            var root = document.RootElement;
            if (root.TryGetProperty("environment", out var env) &&
                env.ValueKind == JsonValueKind.String)
                environment = env.GetString() ?? "Unverified";
            if (root.TryGetProperty("sandboxFiscalMemoryId", out var memory) &&
                memory.ValueKind == JsonValueKind.String)
                memory.TryGetGuid(out memoryId);
        }
        catch (JsonException)
        {
            environment = "Unverified";
        }

        var consent = new SandboxSingleInvoiceConsent(
            owner.Value, taxpayerId, invoiceId, request.SandboxFiscalMemoryId,
            request.InvoiceRevision, request.ApprovedSnapshotSha256,
            request.ExpiresAtUtc, request.OperatorConfirmed, request.Environment);
        var result = SandboxSubmissionAdmissionPolicy.Evaluate(
            new SandboxSubmissionAdmissionContext(
                owner.Value, actor.ActorId, taxpayerId, invoiceId, memoryId,
                invoice.Revision, invoice.State, reviewState,
                invoice.SnapshotSha256, invoice.SnapshotSha256, environment,
                consent, DateTimeOffset.UtcNow));

        return new(InvoiceApiDecision.Success, new(
            result.IsAdmitted, environment, invoice.Revision, invoice.SnapshotSha256,
            result.BlockingCodes.ToArray()));
    }
}
