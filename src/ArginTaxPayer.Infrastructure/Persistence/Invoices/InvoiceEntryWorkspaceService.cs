using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Private, editable wizard data. NOT an issued or finalized tax invoice.</summary>
public sealed class InvoiceEntryWorkspace
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public long Revision { get; set; }
    public int LastCompletedStep { get; set; }
    public DateTime IssueDateUtc { get; set; }
    public string DraftJson { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed record InvoiceEntryLine(Guid? CatalogItemId, string Description,
    decimal Quantity, decimal UnitPriceRial, decimal DiscountPerUnitRial, decimal? VatRatePercent);
public sealed record SaveInvoiceEntryRequest(Guid? Id, Guid TaxpayerId, long ExpectedRevision,
    int LastCompletedStep, DateTime IssueDateUtc, Guid? BuyerPartyId,
    int Type, int Pattern, InvoiceEntryLine[]? Lines);
public sealed record InvoiceEntryView(Guid Id, Guid CustomerUserId, Guid TaxpayerId, long Revision,
    int LastCompletedStep, DateTime IssueDateUtc, DateTime UpdatedAtUtc,
    Guid? BuyerPartyId, int Type, int Pattern, InvoiceEntryLine[] Lines);
public sealed record InvoiceEntryResult(InvoiceApiDecision Decision, InvoiceEntryView? Draft = null);

/// <summary>
/// Draft only, no issuance; authorization is checked on every read/write.
/// JSON is canonicalized from typed server-validated values, not trusted raw input.
/// Optimistic CAS prevents simultaneous tabs from silently overwriting changes.
/// </summary>
public sealed class InvoiceEntryWorkspaceService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    private async Task<(InvoiceApiDecision, Guid)> Authorize(
        string? token, Guid taxpayerId, CancellationToken ct)
    {
        var principal = await scope.ResolveAsync(token, ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
            return (InvoiceApiDecision.Unauthenticated, Guid.Empty);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return (InvoiceApiDecision.Forbidden, Guid.Empty);
        var owner = await scope.ScopedQuery(principal.ActorId, principal.Level1)
            .Where(t => t.Id == taxpayerId &&
                t.Status == ArginTaxPayer.Domain.Taxpayers.TaxpayerStatus.Active)
            .Select(t => t.CustomerUserId).SingleOrDefaultAsync(ct);
        return owner is not { } id || id == Guid.Empty
            ? (InvoiceApiDecision.NotFound, Guid.Empty)
            : (InvoiceApiDecision.Success, id);
    }

    private static bool Valid(SaveInvoiceEntryRequest input)
    {
        if (input.TaxpayerId == Guid.Empty || input.ExpectedRevision < 0 ||
            input.LastCompletedStep is < 0 or > 2 ||
            input.IssueDateUtc.Kind != DateTimeKind.Utc ||
            input.Type is < 1 or > 2 || input.Pattern is < 1 or > 14 ||
            input.Lines is null || input.Lines.Length > 150 ||
            input.Lines.Any(l => l.Description is null || l.Description.Length > 256 ||
                l.Description.Any(char.IsControl) || l.Quantity is < 0 or > 999999999m ||
                l.UnitPriceRial is < 0 or > 999999999999999m ||
                l.DiscountPerUnitRial < 0 || l.DiscountPerUnitRial > l.UnitPriceRial ||
                l.VatRatePercent is < 0 or >= 1000))
            return false;
        return true;
    }

    private static InvoiceEntryView View(InvoiceEntryWorkspace row)
    {
        var value = JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(row.DraftJson)
            ?? throw new InvalidOperationException("Stored draft has no canonical content.");
        return new(row.Id,row.CustomerUserId,row.TaxpayerId,row.Revision,
            row.LastCompletedStep,row.IssueDateUtc,row.UpdatedAtUtc,
            value.BuyerPartyId,value.Type,value.Pattern,value.Lines ?? []);
    }

    public async Task<InvoiceEntryResult> SaveAsync(string? token,
        SaveInvoiceEntryRequest input, CancellationToken ct = default)
    {
        if (input is null || !Valid(input))
            return new(InvoiceApiDecision.Invalid);
        var access = await Authorize(token,input.TaxpayerId,ct);
        if (access.Item1 != InvoiceApiDecision.Success) return new(access.Item1);
        if (input.BuyerPartyId is { } buyer && buyer != Guid.Empty)
        {
            var linked = await db.CustomerPartyLinks.AsNoTracking().AnyAsync(l =>
                l.CustomerUserId == access.Item2 && l.PartyId == buyer &&
                l.UnlinkedAtUtc == null &&
                db.Parties.Any(p => p.Id == buyer && p.Status == PartyStatus.Active),ct);
            if (!linked) return new(InvoiceApiDecision.Invalid);
        }
        if (input.BuyerPartyId == Guid.Empty || input.Lines!.Any(l =>
                l.CatalogItemId == Guid.Empty))
            return new(InvoiceApiDecision.Invalid);
        var productIds = input.Lines.Where(l => l.CatalogItemId.HasValue)
            .Select(l => l.CatalogItemId!.Value).Distinct().ToArray();
        if (productIds.Length > 0)
        {
            var allowedCount = await db.CustomerCatalogItems.AsNoTracking().CountAsync(c =>
                productIds.Contains(c.Id) && c.CustomerUserId == access.Item2 &&
                c.Status == 1,ct);
            if (allowedCount != productIds.Length)
                return new(InvoiceApiDecision.Invalid);
        }

        var now = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(input with { Id = null, ExpectedRevision = 0 });
        if (input.Id is null)
        {
            if (input.ExpectedRevision != 0) return new(InvoiceApiDecision.Conflict);
            var row = new InvoiceEntryWorkspace {
                Id = Guid.NewGuid(),CustomerUserId = access.Item2,
                TaxpayerId = input.TaxpayerId,Revision = 1,
                LastCompletedStep = input.LastCompletedStep,
                IssueDateUtc = input.IssueDateUtc,DraftJson = json,UpdatedAtUtc = now
            };
            db.InvoiceEntryWorkspaces.Add(row);
            await db.SaveChangesAsync(ct);
            return new(InvoiceApiDecision.Success,View(row));
        }
        if (input.Id == Guid.Empty || input.ExpectedRevision < 1)
            return new(InvoiceApiDecision.Invalid);
        var updated = await db.InvoiceEntryWorkspaces.Where(x => x.Id == input.Id &&
            x.CustomerUserId == access.Item2 && x.TaxpayerId == input.TaxpayerId &&
            x.Revision == input.ExpectedRevision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Revision,x => x.Revision + 1)
                .SetProperty(x => x.DraftJson,json)
                .SetProperty(x => x.IssueDateUtc,input.IssueDateUtc)
                .SetProperty(x => x.LastCompletedStep,input.LastCompletedStep)
                .SetProperty(x => x.UpdatedAtUtc,now),ct);
        if (updated != 1) return new(InvoiceApiDecision.Conflict);
        var saved = await db.InvoiceEntryWorkspaces.AsNoTracking()
            .SingleAsync(x => x.Id == input.Id && x.CustomerUserId == access.Item2,ct);
        return new(InvoiceApiDecision.Success,View(saved));
    }

    public async Task<InvoiceEntryResult> GetAsync(string? token,Guid id,
        CancellationToken ct = default)
    {
        var principal = await scope.ResolveAsync(token,ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);
        var allowed = scope.ScopedQuery(principal.ActorId,principal.Level1)
            .Select(x => x.Id);
        var row = await db.InvoiceEntryWorkspaces.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && allowed.Contains(x.TaxpayerId),ct);
        return row is null ? new(InvoiceApiDecision.NotFound)
            : new(InvoiceApiDecision.Success,View(row));
    }

    public async Task<(InvoiceApiDecision Decision, InvoiceEntryView[] Drafts)> ListAsync(
        string? token,Guid? customerId,Guid? taxpayerId,int page,int pageSize,
        CancellationToken ct = default)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100)
            return (InvoiceApiDecision.Invalid,[]);
        var principal = await scope.ResolveAsync(token,ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
            return (InvoiceApiDecision.Unauthenticated,[]);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return (InvoiceApiDecision.Forbidden,[]);
        var permitted = scope.ScopedQuery(principal.ActorId,principal.Level1).Select(x => x.Id);
        var records = db.InvoiceEntryWorkspaces.AsNoTracking()
            .Where(x => permitted.Contains(x.TaxpayerId));
        if (customerId.HasValue) records = records.Where(x => x.CustomerUserId == customerId);
        if (taxpayerId.HasValue) records = records.Where(x => x.TaxpayerId == taxpayerId);
        var items = await records.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id)
            .Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct);
        return (InvoiceApiDecision.Success,items.Select(View).ToArray());
    }
}
