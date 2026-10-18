using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum InvoiceApiDecision
{
    Success, Unauthenticated, Forbidden, NotFound, Invalid, Conflict
}

public sealed record CreateInvoiceDraftRequest(
    DateTime IssueDateUtc, string SellerDisplayName, string? BuyerDisplayName, Guid? BuyerPartyId = null);

public sealed record InvoiceDraftView(
    Guid Id, Guid CustomerUserId, Guid TaxpayerId,
    long Revision, string State, string Subject, DateTime IssueDateUtc,
    DateTime UpdatedAtUtc, string? Kind = null, int? LastCompletedStep = null);

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

        if (input is null || input.BuyerPartyId is null ||
            input.BuyerPartyId == Guid.Empty || input.IssueDateUtc.Kind != DateTimeKind.Utc ||
            !ValidName(input.SellerDisplayName) ||
            input.BuyerDisplayName is not null && !ValidName(input.BuyerDisplayName))
            return new(InvoiceApiDecision.Invalid);

        var seller = await db.TaxpayerProfiles.AsNoTracking()
            .Where(x => x.Id == taxpayerId && x.CustomerUserId == access.CustomerId &&
                x.Status == ArginTaxPayer.Domain.Taxpayers.TaxpayerStatus.Active)
            .Select(x => x.DisplayName).SingleOrDefaultAsync(ct);
        if (seller is null) return new(InvoiceApiDecision.NotFound);
        // Seller is always the selected taxpayer. It must never be edited
        // independently of its fiscal taxpayer identity.
        if (input.SellerDisplayName != seller) return new(InvoiceApiDecision.Invalid);
        string? buyer = null;
        if (input.BuyerPartyId is { } buyerId && buyerId != Guid.Empty)
        {
            buyer = await (from link in db.CustomerPartyLinks.AsNoTracking()
                join party in db.Parties.AsNoTracking() on link.PartyId equals party.Id
                where link.CustomerUserId == access.CustomerId &&
                    link.UnlinkedAtUtc == null && party.Id == buyerId &&
                    party.Status == PartyStatus.Active
                select party.DisplayName).SingleOrDefaultAsync(ct);
            if (buyer is null || buyer != input.BuyerDisplayName)
                return new(InvoiceApiDecision.Invalid);
        }
        else if (input.BuyerDisplayName is not null)
            return new(InvoiceApiDecision.Invalid);

        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        // This invokes real Domain identity/date validation and serializes
        // server-owned fields, not a client-supplied lifecycle or tax ID.
        var aggregate = InvoiceAggregate.CreateDraft(
            id, access.CustomerId, taxpayerId, null, now,
            input.IssueDateUtc,
            new InvoicePartySnapshot(null, seller),
            buyer is null ? null :
                new InvoicePartySnapshot(null, buyer));

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
            buyerPartyId = input.BuyerPartyId,
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

    /// <summary>Read all visible invoices, optionally scoped to one customer or taxpayer.</summary>
    public async Task<(InvoiceApiDecision Decision, InvoiceDraftView[] Data)> ListVisibleAsync(
        string? token, Guid? customerId, Guid? taxpayerId,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > 100000)
            return (InvoiceApiDecision.Invalid, []);
        var principal = await scope.ResolveAsync(token, ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
            return (InvoiceApiDecision.Unauthenticated, []);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return (InvoiceApiDecision.Forbidden, []);

        // The scoped taxpayer SQL predicate independently enforces the live
        // customer relationship and current Level2 ownership / MFA. No client
        // supplied customer ID can extend its reach.
        var allowed = scope.ScopedQuery(principal.ActorId, principal.Level1)
            .Select(x => x.Id);
        var documents = db.InvoiceDocuments.AsNoTracking()
            .Where(x => allowed.Contains(x.TaxpayerId))
            .Select(x => new {
                x.Id, x.CustomerUserId, x.TaxpayerId, x.Revision,
                x.IssueDateUtc, x.UpdatedAtUtc,
                Lifecycle = (int?)x.State, SubjectValue = (int?)x.Subject,
                Source = 0, Step = (int?)null
            });
        var working = db.InvoiceEntryWorkspaces.AsNoTracking()
            .Where(x => allowed.Contains(x.TaxpayerId))
            .Select(x => new {
                x.Id, x.CustomerUserId, x.TaxpayerId, x.Revision,
                x.IssueDateUtc, x.UpdatedAtUtc,
                Lifecycle = (int?)null, SubjectValue = (int?)null,
                Source = 1, Step = (int?)x.LastCompletedStep
            });
        var query = documents.Concat(working);
        if (customerId.HasValue)
            query = query.Where(x => x.CustomerUserId == customerId.Value);
        if (taxpayerId.HasValue)
            query = query.Where(x => x.TaxpayerId == taxpayerId.Value);
        var rows = await query.OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).ToArrayAsync(ct);
        return (InvoiceApiDecision.Success, rows.Select(x =>
            new InvoiceDraftView(x.Id,x.CustomerUserId,x.TaxpayerId,x.Revision,
                x.Source == 1 ? "WorkingDraft" :
                    ((InvoiceLifecycleState)x.Lifecycle!.Value).ToString(),
                x.Source == 1 ? "Original" :
                    ((InvoiceSubject)x.SubjectValue!.Value).ToString(),
                x.IssueDateUtc,x.UpdatedAtUtc,
                x.Source == 1 ? "workspace" : null, x.Step)).ToArray());
    }

    private static InvoiceDraftView View(StoredInvoice invoice) =>
        new(invoice.Id, invoice.CustomerUserId, invoice.TaxpayerId,
            invoice.Revision, invoice.State.ToString(), invoice.Subject.ToString(),
            invoice.IssueDateUtc, invoice.UpdatedAtUtc);

    private static bool ValidName(string? value) =>
        value is { Length: > 0 and <= 256 } &&
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
}
