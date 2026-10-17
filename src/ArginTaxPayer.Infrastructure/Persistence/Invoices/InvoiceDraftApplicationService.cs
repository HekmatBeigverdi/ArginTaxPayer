using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum InvoiceApiDecision
{
    Success, Unauthenticated, Forbidden, NotFound, Invalid, Conflict
}

public sealed record CreateInvoiceDraftRequest(
    DateTime IssueDateUtc, string SellerDisplayName, string? BuyerDisplayName);

public sealed record InvoiceDraftView(
    Guid Id, Guid CustomerUserId, Guid TaxpayerId,
    long Revision, string State, string Subject, DateTime IssueDateUtc,
    DateTime UpdatedAtUtc);

public sealed record InvoiceDraftResult(InvoiceApiDecision Decision,
    InvoiceDraftView? Invoice = null);

/// <summary>
/// Phase07 safe application orchestration. Trusted live session and taxpayer
/// authorization are re-evaluated for every operation; no caller-provided
/// customer identity is accepted. Never issues or transmits tax invoices.
/// </summary>
public sealed class InvoiceDraftApplicationService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope,
    EfInvoiceAtomicWriter writer)
{
    private async Task<(InvoiceApiDecision Decision, Guid CustomerId)> AuthorizeAsync(
        string? token, Guid taxpayerId, CancellationToken ct)
    {
        var actor = await scope.ResolveAsync(token, ct);
        if (actor.Decision == TaxpayerScopeDecision.Unauthenticated)
            return (InvoiceApiDecision.Unauthenticated, Guid.Empty);
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
            return (InvoiceApiDecision.Forbidden, Guid.Empty);

        // SQL scope predicate checks live user role, MFA and current ownership.
        var owner = await scope.ScopedQuery(actor.ActorId, actor.Level1)
            .Where(p => p.Id == taxpayerId)
            .Select(p => p.CustomerUserId)
            .SingleOrDefaultAsync(ct);

        if (owner is null || owner == Guid.Empty)
            return (InvoiceApiDecision.NotFound, Guid.Empty);
        return (InvoiceApiDecision.Success, owner.Value);
    }

    public async Task<InvoiceDraftResult> CreateAsync(
        string? token, Guid taxpayerId, CreateInvoiceDraftRequest? input,
        CancellationToken ct = default)
    {
        var access = await AuthorizeAsync(token, taxpayerId, ct);
        if (access.Decision != InvoiceApiDecision.Success)
            return new(access.Decision);

        if (input is null || input.IssueDateUtc.Kind != DateTimeKind.Utc ||
            !ValidName(input.SellerDisplayName) ||
            input.BuyerDisplayName is not null && !ValidName(input.BuyerDisplayName))
            return new(InvoiceApiDecision.Invalid);

        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        // This invokes real Domain identity/date validation and serializes
        // server-owned fields, not a client-supplied lifecycle or tax ID.
        var aggregate = InvoiceAggregate.CreateDraft(
            id, access.CustomerId, taxpayerId, null, now,
            input.IssueDateUtc,
            new InvoicePartySnapshot(null, input.SellerDisplayName),
            input.BuyerDisplayName is null ? null :
                new InvoicePartySnapshot(null, input.BuyerDisplayName));

        var snapshot = JsonSerializer.Serialize(new
        {
            id = aggregate.Id,
            customerUserId = aggregate.CustomerUserId,
            taxpayerId = aggregate.TaxpayerId,
            revision = aggregate.Revision,
            state = (int)aggregate.State,
            subject = (int)aggregate.SubjectSelection.Subject,
            issueDateUtc = aggregate.IssueDateUtc,
            seller = aggregate.Seller.DisplayName,
            buyer = aggregate.Buyer?.DisplayName,
            lines = Array.Empty<object>()
        });
        // Scoped IDs must be acquired from authorized query above. The
        // application makes no claims of legal invoice-rule readiness.
        var created = await writer.CreateDraftAsync(id, access.CustomerId,
            taxpayerId, input.IssueDateUtc, snapshot, now, ct);
        return created
            ? new(InvoiceApiDecision.Success, View(
                new StoredInvoice {
                    Id = id, CustomerUserId = access.CustomerId,
                    TaxpayerId = taxpayerId, Revision = 1,
                    State = InvoiceLifecycleState.Draft,
                    Subject = InvoiceSubject.Original,
                    IssueDateUtc = input.IssueDateUtc, UpdatedAtUtc = now
                }))
            : new(InvoiceApiDecision.Conflict);
    }

    public async Task<InvoiceDraftResult> GetAsync(
        string? token, Guid taxpayerId, Guid invoiceId,
        CancellationToken ct = default)
    {
        var access = await AuthorizeAsync(token, taxpayerId, ct);
        if (access.Decision != InvoiceApiDecision.Success)
            return new(access.Decision);
        var invoice = await db.InvoiceDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == invoiceId &&
                x.TaxpayerId == taxpayerId &&
                x.CustomerUserId == access.CustomerId, ct);
        return invoice is null ? new(InvoiceApiDecision.NotFound)
            : new(InvoiceApiDecision.Success, View(invoice));
    }

    public async Task<(InvoiceApiDecision Decision, InvoiceDraftView[] Data)> ListAsync(
        string? token, Guid taxpayerId, CancellationToken ct = default)
    {
        var access = await AuthorizeAsync(token, taxpayerId, ct);
        if (access.Decision != InvoiceApiDecision.Success)
            return (access.Decision, Array.Empty<InvoiceDraftView>());
        var invoices = await db.InvoiceDocuments.AsNoTracking()
            .Where(x => x.CustomerUserId == access.CustomerId &&
                x.TaxpayerId == taxpayerId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(100)
            .ToArrayAsync(ct);
        return (InvoiceApiDecision.Success, invoices.Select(View).ToArray());
    }

    private static InvoiceDraftView View(StoredInvoice invoice) =>
        new(invoice.Id, invoice.CustomerUserId, invoice.TaxpayerId,
            invoice.Revision, invoice.State.ToString(), invoice.Subject.ToString(),
            invoice.IssueDateUtc, invoice.UpdatedAtUtc);

    private static bool ValidName(string? value) =>
        value is { Length: > 0 and <= 256 } &&
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
}
