using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record Phase10ReviewView(string State, long Revision);
public sealed record Phase10ReviewReadResult(InvoiceApiDecision Decision, Phase10ReviewView? Review = null);

/// <summary>Read only, scoped view of the independent internal review cursor.</summary>
public sealed class Phase10ReviewReadService(ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<Phase10ReviewReadResult> ReadAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct = default)
    {
        if (taxpayerId == Guid.Empty || invoiceId == Guid.Empty)
            return new(InvoiceApiDecision.Invalid);
        var actor = await scope.ResolveAsync(bearer, ct);
        if (actor.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated);
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);
        var owner = await scope.ScopedQuery(actor.ActorId, actor.Level1)
            .Where(t => t.Id == taxpayerId).Select(t => t.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if (owner is null || owner == Guid.Empty ||
            !await db.InvoiceDocuments.AsNoTracking().AnyAsync(i => i.Id == invoiceId &&
                i.TaxpayerId == taxpayerId && i.CustomerUserId == owner.Value, ct))
            return new(InvoiceApiDecision.NotFound);
        var current = await db.InvoiceReviews.AsNoTracking()
            .Where(r => r.InvoiceId == invoiceId && r.TaxpayerId == taxpayerId &&
                r.CustomerUserId == owner.Value)
            .Select(r => new { r.State, r.Revision }).SingleOrDefaultAsync(ct);
        return new(InvoiceApiDecision.Success,
            new(current?.State.ToString() ?? InvoiceReviewState.Draft.ToString(),
                current?.Revision ?? 1));
    }
}
