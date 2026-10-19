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

public sealed record InvoiceEntryLine(Guid? CatalogItemId, string Description,
    decimal Quantity, decimal UnitPriceRial, decimal DiscountPerUnitRial, decimal? VatRatePercent);
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
            gross = checked(gross + line.Quantity * line.UnitPriceRial);
            discount = checked(discount + line.Quantity * line.DiscountPerUnitRial);
            vat = checked(vat + line.Quantity *
                (line.UnitPriceRial - line.DiscountPerUnitRial) *
                (line.VatRatePercent ?? 0m) / 100m);
        }
        gross = decimal.Round(gross, 2, MidpointRounding.AwayFromZero);
        discount = decimal.Round(discount, 2, MidpointRounding.AwayFromZero);
        vat = decimal.Round(vat, 2, MidpointRounding.AwayFromZero);
        var subtotal = checked(gross - discount);
        return new(gross, discount, subtotal, vat, checked(subtotal + vat));
    }

    public static bool IsConsistent(InvoiceDraftSettlementMethod method,
        decimal? cashRial, decimal? creditRial, decimal grandTotalRial)
    {
        if (!Enum.IsDefined(method) || grandTotalRial < 0 ||
            cashRial is < 0 || creditRial is < 0) return false;
        if (method == InvoiceDraftSettlementMethod.Unknown)
            return (cashRial is null or 0m) && (creditRial is null or 0m);
        // Both amounts may be absent while a working draft is incomplete.
        if (cashRial is null && creditRial is null) return true;
        if (cashRial is null || creditRial is null) return false;
        if (cashRial.Value + creditRial.Value != grandTotalRial) return false;
        return method switch
        {
            InvoiceDraftSettlementMethod.Cash => creditRial.Value == 0 && cashRial.Value == grandTotalRial,
            InvoiceDraftSettlementMethod.Credit => cashRial.Value == 0 && creditRial.Value == grandTotalRial,
            InvoiceDraftSettlementMethod.CashAndCredit => cashRial.Value > 0 && creditRial.Value > 0,
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
    decimal? GoldCommissionRial = null, decimal? GoldTotalFeeProfitRial = null);
public sealed record InvoiceEntryView(Guid Id, Guid CustomerUserId, Guid TaxpayerId, long Revision,
    int LastCompletedStep, DateTime IssueDateUtc, DateTime UpdatedAtUtc,
    Guid? BuyerPartyId, int Type, int Pattern, InvoiceEntryLine[] Lines,
    string? BuyerDisplayName, string? InvoiceDescription,
    InvoiceDraftSettlementMethod SettlementMethod, decimal? CashAmountRial,
    decimal? CreditAmountRial, InvoiceDraftFinancialSummary FinancialSummary,
    string? ContractNumber = null, string? ServiceBillId = null, int? FlightType = null,
    decimal? GoldConstructionFeeRial = null, decimal? GoldSellerProfitRial = null,
    decimal? GoldCommissionRial = null, decimal? GoldTotalFeeProfitRial = null);
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
            value.GoldCommissionRial,value.GoldTotalFeeProfitRial);
    }

    public async Task<InvoiceEntryResult> SaveAsync(string? token,
        SaveInvoiceEntryRequest input, CancellationToken ct = default)
    {
        if (input is null || !Valid(input))
            return new(InvoiceApiDecision.Invalid);
        var access = await Authorize(token,input.TaxpayerId,ct);
        if (access.Item1 != InvoiceApiDecision.Success) return new(access.Item1);
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
        var json = JsonSerializer.Serialize(input with {
            Id = null, ExpectedRevision = 0, BuyerDisplayName = selectedBuyerName
        });
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
