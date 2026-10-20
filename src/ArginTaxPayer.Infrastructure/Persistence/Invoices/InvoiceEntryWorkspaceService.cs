using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
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

public sealed class InvoiceEntryWorkspaceRevision
{
    public Guid WorkspaceId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public long Revision { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = "save";
    public string Reason { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
}

public sealed record InvoiceWorkspaceRevisionView(long Revision, Guid ActorUserId,
    string Action, string Reason, DateTime OccurredAtUtc, string ActorDisplayName);
public sealed record InvoiceWorkspaceHistoryResult(InvoiceApiDecision Decision,
    InvoiceWorkspaceRevisionView[] History);

public sealed record InvoiceEntryLine(Guid? CatalogItemId, string Description,
    decimal Quantity, decimal UnitPriceRial, decimal DiscountPerUnitRial, decimal? VatRatePercent,
    Guid? UnitId = null, string? CatalogCodeSnapshot = null);
public enum InvoiceDraftSettlementMethod { Unknown = 0, Cash = 1, Credit = 2, CashAndCredit = 3 }

public sealed record InvoiceDraftFinancialSummary(decimal GrossRial, decimal DiscountRial,
    decimal AfterDiscountRial, decimal PreviewVatRial, decimal PreviewGrandTotalRial);

public static class InvoiceDraftSettlementPolicy
{
    public static InvoiceDraftFinancialSummary Calculate(IReadOnlyList<InvoiceEntryLine> lines)
    {
        decimal gross = 0, discount = 0, vat = 0;
        foreach (var line in lines)
        {
            // Quantize each invoice line once using decimal; aggregate rounded
            // amounts, never floating-point UI totals or client-provided sums.
            var grossLine = decimal.Round(checked(line.Quantity * line.UnitPriceRial),
                2, MidpointRounding.AwayFromZero);
            var discountLine = decimal.Round(checked(line.Quantity * line.DiscountPerUnitRial),
                2, MidpointRounding.AwayFromZero);
            var baseLine = checked(grossLine - discountLine);
            var vatLine = decimal.Round(checked(
                baseLine * (line.VatRatePercent ?? 0m) / 100m),
                2, MidpointRounding.AwayFromZero);
            gross = checked(gross + grossLine);
            discount = checked(discount + discountLine);
            vat = checked(vat + vatLine);
        }
        var subtotal = checked(gross - discount);
        return new(gross, discount, subtotal, vat, checked(subtotal + vat));
    }

    public static bool IsConsistent(InvoiceDraftSettlementMethod method,
        decimal? cashRial, decimal? creditRial, decimal grandTotalRial)
    {
        if (!Enum.IsDefined(method) || grandTotalRial < 0 ||
            cashRial is < 0 || creditRial is < 0) return false;
        if (method == InvoiceDraftSettlementMethod.Unknown)
            return cashRial is null && creditRial is null;
        // Incomplete drafts may omit amounts; completed settlement is checked
        // separately at the step boundary, and must be fully reconciled.
        if (cashRial is null && creditRial is null) return true;
        if (cashRial is null || creditRial is null) return false;
        if (cashRial.Value + creditRial.Value != grandTotalRial) return false;
        return method switch
        {
            InvoiceDraftSettlementMethod.Cash => creditRial.Value == 0 && cashRial.Value == grandTotalRial,
            InvoiceDraftSettlementMethod.Credit => cashRial.Value == 0 && creditRial.Value == grandTotalRial,
            InvoiceDraftSettlementMethod.CashAndCredit => cashRial.Value > 0 && creditRial.Value > 0 &&
                cashRial.Value < grandTotalRial && creditRial.Value < grandTotalRial,
            _ => false
        };
    }
}

public sealed record SaveInvoiceEntryRequest(Guid? Id, Guid TaxpayerId, long ExpectedRevision,
    int LastCompletedStep, DateTime IssueDateUtc, Guid? BuyerPartyId,
    int Type, int Pattern, InvoiceEntryLine[]? Lines,
    string? BuyerDisplayName = null, string? InvoiceDescription = null,
    InvoiceDraftSettlementMethod SettlementMethod = InvoiceDraftSettlementMethod.Unknown,
    decimal? CashAmountRial = null, decimal? CreditAmountRial = null,
    string? ContractNumber = null, string? ServiceBillId = null, int? FlightType = null,
    decimal? GoldConstructionFeeRial = null, decimal? GoldSellerProfitRial = null,
    decimal? GoldCommissionRial = null, decimal? GoldTotalFeeProfitRial = null,
    Guid? FiscalMemoryId = null, string? InternalReviewState = null,
    string? InternalReviewReason = null, Guid? SubmittedByUserId = null,
    Guid? ReviewedByUserId = null, DateTime? ReviewedAtUtc = null);
public sealed record InvoiceEntryView(Guid Id, Guid CustomerUserId, Guid TaxpayerId, long Revision,
    int LastCompletedStep, DateTime IssueDateUtc, DateTime UpdatedAtUtc,
    Guid? BuyerPartyId, int Type, int Pattern, InvoiceEntryLine[] Lines,
    string? BuyerDisplayName, string? InvoiceDescription,
    InvoiceDraftSettlementMethod SettlementMethod, decimal? CashAmountRial,
    decimal? CreditAmountRial, InvoiceDraftFinancialSummary FinancialSummary,
    string? ContractNumber = null, string? ServiceBillId = null, int? FlightType = null,
    decimal? GoldConstructionFeeRial = null, decimal? GoldSellerProfitRial = null,
    decimal? GoldCommissionRial = null, decimal? GoldTotalFeeProfitRial = null,
    Guid? FiscalMemoryId = null, string? InternalReviewState = null,
    string? InternalReviewReason = null, Guid? SubmittedByUserId = null,
    Guid? ReviewedByUserId = null, DateTime? ReviewedAtUtc = null);
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
            input.Type is < 1 or > 2 ||
            OfficialV79InvoicePatternMatrix.Create().Evaluate(
                new InvoiceTypePattern((ElectronicInvoiceType)input.Type,input.Pattern),
                input.IssueDateUtc) != InvoicePatternDecision.AllowedByReferencedEdition ||
            input.Lines is null || input.Lines.Length > 150 ||
            input.InvoiceDescription is { Length: > 1500 } ||
            (input.InvoiceDescription?.Any(c => char.IsControl(c) &&
                c != '\r' && c != '\n' && c != '\t') ?? false) ||
            input.Lines.Any(l => l.Description is null || l.Description.Length > 256 ||
                l.Description.Any(char.IsControl) || l.Quantity is < 0 or > 999999999m ||
                l.UnitPriceRial is < 0 or > 999999999999999m ||
                l.DiscountPerUnitRial < 0 || l.DiscountPerUnitRial > l.UnitPriceRial ||
                l.VatRatePercent is < 0 or >= 1000))
            return false;
        static bool Digits(string? v,int max) => v is {Length: > 0} &&
            v.Length <= max && v.All(c => c is >= '0' and <= '9');
        if (input.ContractNumber is not null &&
            (input.Type != 1 || input.Pattern != 4 || !Digits(input.ContractNumber,12)) ||
            input.ServiceBillId is not null &&
            (input.Type != 1 || input.Pattern != 5 || !Digits(input.ServiceBillId,19)) ||
            input.FlightType is not null &&
            (input.Type != 1 || input.Pattern != 6 || input.FlightType is not (1 or 2)) ||
            input.Pattern != 3 &&
            (input.GoldConstructionFeeRial is not null || input.GoldSellerProfitRial is not null ||
             input.GoldCommissionRial is not null || input.GoldTotalFeeProfitRial is not null) ||
            new decimal?[] { input.GoldConstructionFeeRial, input.GoldSellerProfitRial,
                input.GoldCommissionRial, input.GoldTotalFeeProfitRial }.Any(v => v is < 0 or > 999999999999999m))
            return false;
        // Workspaces may be saved incomplete, but completed header must include
        // the official condition-specific fields before moving past step zero.
        if (input.LastCompletedStep >= 1 &&
            (input.Type == 1 && input.Pattern == 4 && !Digits(input.ContractNumber,12) ||
             input.Type == 1 && input.Pattern == 5 && !Digits(input.ServiceBillId,19) ||
             input.Type == 1 && input.Pattern == 6 && input.FlightType is not (1 or 2) ||
             input.Pattern == 3 &&
             (input.GoldConstructionFeeRial is null || input.GoldSellerProfitRial is null ||
              input.GoldCommissionRial is null || input.GoldTotalFeeProfitRial is null)))
            return false;
        try
        {
            var summary = InvoiceDraftSettlementPolicy.Calculate(input.Lines);
            if (input.LastCompletedStep == 2 &&
                (input.SettlementMethod == InvoiceDraftSettlementMethod.Unknown ||
                 input.CashAmountRial is null || input.CreditAmountRial is null))
                return false;
            return InvoiceDraftSettlementPolicy.IsConsistent(input.SettlementMethod,
                input.CashAmountRial, input.CreditAmountRial, summary.PreviewGrandTotalRial);
        }
        catch (OverflowException) { return false; }
    }

    private static InvoiceEntryView View(InvoiceEntryWorkspace row)
    {
        var value = JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(row.DraftJson)
            ?? throw new InvalidOperationException("Stored draft has no canonical content.");
        return new(row.Id,row.CustomerUserId,row.TaxpayerId,row.Revision,
            row.LastCompletedStep,row.IssueDateUtc,row.UpdatedAtUtc,
            value.BuyerPartyId,value.Type,value.Pattern,value.Lines ?? [],
            value.BuyerDisplayName, value.InvoiceDescription, value.SettlementMethod,
            value.CashAmountRial, value.CreditAmountRial,
            InvoiceDraftSettlementPolicy.Calculate(value.Lines ?? []),
            value.ContractNumber,value.ServiceBillId,value.FlightType,
            value.GoldConstructionFeeRial,value.GoldSellerProfitRial,
            value.GoldCommissionRial,value.GoldTotalFeeProfitRial,value.FiscalMemoryId,
            value.InternalReviewState,value.InternalReviewReason,value.SubmittedByUserId,
            value.ReviewedByUserId,value.ReviewedAtUtc);
    }

    public async Task<InvoiceEntryResult> SaveAsync(string? token,
        SaveInvoiceEntryRequest input, CancellationToken ct = default)
    {
        if (input is null || !Valid(input))
            return new(InvoiceApiDecision.Invalid);
        var access = await Authorize(token,input.TaxpayerId,ct);
        if (access.Item1 != InvoiceApiDecision.Success) return new(access.Item1);
        // Fiscal memory must belong to the selected, currently accessible seller.
        // It remains an optional draft reference until authoritative verification
        // and activation are available in later phases.
        if (input.FiscalMemoryId is { } memoryId &&
            (memoryId == Guid.Empty || !await db.FiscalMemories.AsNoTracking().AnyAsync(
                m => m.Id == memoryId && m.TaxpayerId == input.TaxpayerId, ct)))
            return new(InvoiceApiDecision.Invalid);
        string? selectedBuyerName = null;
        if (input.BuyerPartyId is { } buyer && buyer != Guid.Empty)
        {
            selectedBuyerName = await (from l in db.CustomerPartyLinks.AsNoTracking()
                join party in db.Parties.AsNoTracking() on l.PartyId equals party.Id
                where l.CustomerUserId == access.Item2 && l.PartyId == buyer &&
                    l.UnlinkedAtUtc == null && party.Status == PartyStatus.Active
                select party.DisplayName).SingleOrDefaultAsync(ct);
            if (selectedBuyerName is null) return new(InvoiceApiDecision.Invalid);
        }
        if (input.BuyerPartyId == Guid.Empty || input.Lines!.Any(l =>
                l.CatalogItemId == Guid.Empty))
            return new(InvoiceApiDecision.Invalid);
        // Rebuild official unit + internal item code snapshots from the tenant's
        // catalog on first selection; never trust client-provided snapshot metadata.
        var productIds = input.Lines!.Where(l => l.CatalogItemId.HasValue)
            .Select(l => l.CatalogItemId!.Value).Distinct().ToArray();
        var catalogItems = await db.CustomerCatalogItems.AsNoTracking()
            .Where(c => productIds.Contains(c.Id) && c.CustomerUserId == access.Item2)
            .Select(c => new { c.Id, c.InternalCode, c.BaseUnitId, c.Status })
            .ToDictionaryAsync(c => c.Id, ct);
        if (productIds.Any(id => !catalogItems.ContainsKey(id)))
            return new(InvoiceApiDecision.Invalid);
        var original = input.Id is { } existingId
            ? await db.InvoiceEntryWorkspaces.AsNoTracking()
                .Where(w => w.Id == existingId && w.CustomerUserId == access.Item2)
                .Select(w => w.DraftJson).SingleOrDefaultAsync(ct)
            : null;
        var previousLines = original is null ? Array.Empty<InvoiceEntryLine>() :
            JsonSerializer.Deserialize<SaveInvoiceEntryRequest>(original)?.Lines ?? [];
        var canonicalLines = new List<InvoiceEntryLine>(input.Lines.Length);
        foreach (var line in input.Lines)
        {
            if (line.CatalogItemId is not { } itemId)
            {
                if (line.UnitId is not null || line.CatalogCodeSnapshot is not null)
                    return new(InvoiceApiDecision.Invalid);
                canonicalLines.Add(line);
                continue;
            }
            var item = catalogItems[itemId];
            var previous = previousLines.FirstOrDefault(l => l.CatalogItemId == itemId &&
                l.UnitId is not null && l.CatalogCodeSnapshot is not null);
            if (item.Status != 1 && previous is null)
                return new(InvoiceApiDecision.Invalid);
            // Snapshot is frozen for a previously selected catalog item, including
            // when its live title/code/unit changes after draft creation.
            var unit = previous?.UnitId ?? item.BaseUnitId;
            var code = previous?.CatalogCodeSnapshot ?? item.InternalCode;
            if (line.UnitId is { } requestedUnit && requestedUnit != unit)
                return new(InvoiceApiDecision.Invalid);
            canonicalLines.Add(line with {
                UnitId = unit,
                CatalogCodeSnapshot = code
            });
        }

        var now = DateTime.UtcNow;
        // A draft edit always invalidates a prior internal approval. Review
        // metadata is server-owned and may never be forged through the save API.
        var json = JsonSerializer.Serialize(input with {
            Id = null, ExpectedRevision = 0, BuyerDisplayName = selectedBuyerName,
            Lines = canonicalLines.ToArray(), InternalReviewState = "Draft",
            InternalReviewReason = null, SubmittedByUserId = null,
            ReviewedByUserId = null, ReviewedAtUtc = null
        });
        // History rows and the latest draft must commit atomically. A rejected
        // ExpectedRevision never writes an audit event or overwrites a draft.
        var principal = await scope.ResolveAsync(token,ct);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            InvoiceEntryWorkspace saved;
            if (input.Id is null)
            {
                if (input.ExpectedRevision != 0) return new InvoiceEntryResult(InvoiceApiDecision.Conflict);
                saved = new InvoiceEntryWorkspace {
                    Id = Guid.NewGuid(),CustomerUserId = access.Item2,
                    TaxpayerId = input.TaxpayerId,Revision = 1,
                    LastCompletedStep = input.LastCompletedStep,
                    IssueDateUtc = input.IssueDateUtc,DraftJson = json,UpdatedAtUtc = now
                };
                db.InvoiceEntryWorkspaces.Add(saved);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                if (input.Id == Guid.Empty || input.ExpectedRevision < 1)
                    return new InvoiceEntryResult(InvoiceApiDecision.Invalid);
                var updated = await db.InvoiceEntryWorkspaces.Where(x => x.Id == input.Id &&
                    x.CustomerUserId == access.Item2 && x.TaxpayerId == input.TaxpayerId &&
                    x.Revision == input.ExpectedRevision)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Revision,x => x.Revision + 1)
                        .SetProperty(x => x.DraftJson,json)
                        .SetProperty(x => x.IssueDateUtc,input.IssueDateUtc)
                        .SetProperty(x => x.LastCompletedStep,input.LastCompletedStep)
                        .SetProperty(x => x.UpdatedAtUtc,now),ct);
                if (updated != 1) return new InvoiceEntryResult(InvoiceApiDecision.Conflict);
                saved = await db.InvoiceEntryWorkspaces.AsNoTracking()
                    .SingleAsync(x => x.Id == input.Id && x.CustomerUserId == access.Item2,ct);
            }
            db.InvoiceEntryWorkspaceRevisions.Add(new InvoiceEntryWorkspaceRevision {
                WorkspaceId = saved.Id, CustomerUserId = access.Item2,
                TaxpayerId = input.TaxpayerId, Revision = saved.Revision,
                ActorUserId = principal.ActorId, Action = input.Id is null ? "create" : "save",
                Reason = input.Id is null ? "ایجاد پیش‌نویس" : "ذخیره تغییرات پیش‌نویس",
                SnapshotJson = json, OccurredAtUtc = now
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new InvoiceEntryResult(InvoiceApiDecision.Success,View(saved));
        });
    }

    public async Task<InvoiceWorkspaceHistoryResult> HistoryAsync(
        string? token, Guid workspaceId, CancellationToken ct = default)
    {
        var principal = await scope.ResolveAsync(token,ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated,[]);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden,[]);
        var allowed = scope.ScopedQuery(principal.ActorId,principal.Level1)
            .Select(t=>t.Id);
        var exists = await db.InvoiceEntryWorkspaces.AsNoTracking()
            .AnyAsync(w=>w.Id==workspaceId && allowed.Contains(w.TaxpayerId),ct);
        if (!exists) return new(InvoiceApiDecision.NotFound,[]);
        var entries = await db.InvoiceEntryWorkspaceRevisions.AsNoTracking()
            .Where(h=>h.WorkspaceId==workspaceId && allowed.Contains(h.TaxpayerId))
            .OrderByDescending(h=>h.OccurredAtUtc).ThenByDescending(h=>h.Revision)
            .Take(500).ToArrayAsync(ct);
        var actorIds = entries.Select(h=>h.ActorUserId).Distinct().ToArray();
        var names = await db.SoftwareUserProfiles.AsNoTracking()
            .Where(p=>actorIds.Contains(p.UserId))
            .Select(p=>new {p.UserId,p.FirstName,p.LastName})
            .ToDictionaryAsync(p=>p.UserId,p=>
                (p.FirstName+" "+p.LastName).Trim(),ct);
        var history = entries.Select(h=>new InvoiceWorkspaceRevisionView(
            h.Revision,h.ActorUserId,h.Action,h.Reason,h.OccurredAtUtc,
            names.TryGetValue(h.ActorUserId,out var name) && !string.IsNullOrWhiteSpace(name)
                ? name : "کاربر ثبت‌شده")).ToArray();
        return new(InvoiceApiDecision.Success,history);
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
