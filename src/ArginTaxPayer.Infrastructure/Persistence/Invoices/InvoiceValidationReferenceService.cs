using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record InvoiceUiFinding(
    string Key, string Severity, string FieldPath, string Message);
public sealed record InvoiceUiValidation(
    Guid DraftId, long Revision, string Edition,
    bool IsSubmissionAuthorized, IReadOnlyList<InvoiceUiFinding> Findings);
public sealed record InvoiceUiValidationResult(
    InvoiceApiDecision Decision, InvoiceUiValidation? Validation = null);
public sealed record InvoiceUiReference(
    Guid Id, string Subject, string State, DateTime IssueDateUtc,
    long Revision, bool FiscalIdentifierVerified, string? FiscalIdentifier);
public sealed record InvoiceUiReferenceResult(
    InvoiceApiDecision Decision, IReadOnlyList<InvoiceUiReference>? References = null);

/// <summary>
/// UX preflight and scoped reference discovery, not legal issuance readiness.
/// External tax registration and predecessor eligibility cannot be inferred
/// from a local record. No fabricated tax identifier may enter the UI.
/// </summary>
public sealed class InvoiceValidationReferenceService(
    InvoiceEntryWorkspaceService drafts, ArginTaxPayerDbContext db,
    TaxpayerScopeService scope)
{
    public async Task<InvoiceUiValidationResult> ValidateAsync(
        string? token, Guid draftId, CancellationToken ct = default)
    {
        var read = await drafts.GetAsync(token,draftId,ct);
        if (read.Decision != InvoiceApiDecision.Success || read.Draft is null)
            return new(read.Decision);
        var draft = read.Draft;
        var findings = new List<InvoiceUiFinding>();
        void Add(string key, string field, string message, bool warning = false) =>
            findings.Add(new(key,warning ? "warning" : "error",field,message));

        var matrix = OfficialV79InvoicePatternMatrix.Create();
        var selection = new InvoiceTypePattern((ElectronicInvoiceType)draft.Type,draft.Pattern);
        if (matrix.Evaluate(selection,draft.IssueDateUtc) !=
            InvoicePatternDecision.AllowedByReferencedEdition)
            Add("invoice.pattern.unsupported","header.inp",
                "ترکیب نوع و الگوی صورتحساب در نسخه مستند فعلی پشتیبانی نمی‌شود.");
        if (draft.BuyerPartyId is null && draft.Type == 1 &&
            draft.Pattern is not (7 or 11))
            Add("invoice.header.buyer.required","header.buyer",
                "خریدار را از فهرست خریداران همین مشتری انتخاب کنید.");
        if (draft.Lines.Length == 0)
            Add("invoice.lines.required","body",
                "حداقل یک ردیف کالا یا خدمت برای تکمیل پیش‌نویس لازم است.");
        foreach (var (line,index) in draft.Lines.Select((value,index)=>(value,index)))
        {
            if (string.IsNullOrWhiteSpace(line.Description))
                Add("invoice.line.description.required",$"body[{index}].description",
                    $"شرح ردیف {index + 1} را وارد کنید.");
            if (line.Quantity <= 0)
                Add("invoice.line.quantity.positive",$"body[{index}].quantity",
                    $"مقدار ردیف {index + 1} باید بزرگ‌تر از صفر باشد.");
            if (line.VatRatePercent is null)
                Add("invoice.line.vat.unverified",$"body[{index}].vatRate",
                    $"نرخ مالیات ردیف {index + 1} هنوز مشخص و تأیید نشده است.",true);
        }
        // The catalog's InternalCode is NOT the sstid/stid field on a
        // taxpayer-system invoice. A 13-digit official identifier must be
        // mapped to each selected item for its effective issue date.
        foreach(var (line,index) in draft.Lines.Select((value,index)=>(value,index)))
        {
            if(line.CatalogItemId is not { } itemId)
            {
                Add("invoice.line.official_catalog_missing",
                    $"body[{index}].sstid",
                    $"ردیف {index+1}: کالای ثبت‌شده در کاتالوگ انتخاب نشده و شناسه ۱۳ رقمی رسمی قابل تعیین نیست.",true);
                continue;
            }
            var official = await db.CatalogOfficialMappings.AsNoTracking()
                .Where(m=>m.ItemId==itemId &&
                    m.EffectiveFromUtc<=draft.IssueDateUtc &&
                    (m.EffectiveToUtc==null||m.EffectiveToUtc>draft.IssueDateUtc))
                .Join(db.OfficialGoodsServiceEntries.AsNoTracking(),
                    m=>m.OfficialEntryId,e=>e.Id,(m,e)=>new{m,e})
                .Where(x=>x.e.VerificationStatus==2 &&
                    x.e.Code.Length==13 &&
                    db.CatalogReferenceReleases.Any(r=>
                        r.Id==x.e.ReleaseId&&r.State==3))
                .Select(x=>x.e.Code).Take(2).ToArrayAsync(ct);
            if(official.Length!=1)
                Add("invoice.line.official_identifier_missing",
                    $"body[{index}].sstid",
                    $"ردیف {index+1}: نگاشت یکتای شناسه رسمی ۱۳ رقمیِ فعال تأیید نشده است. در کالاها و خدمات، بخش «نگاشت» را تکمیل کنید.",true);
        }
        if (draft.FinancialSummary.PreviewGrandTotalRial <= 0)
            Add("invoice.totals.positive","footer.total",
                "جمع پیش‌نویس باید بزرگ‌تر از صفر باشد.");
        if (draft.SettlementMethod == InvoiceDraftSettlementMethod.Unknown)
            Add("invoice.settlement.unknown","footer.settlement",
                "روش تسویه هنوز تعیین نشده است.",true);

        // V7.9 local catalogue edition is not proven effective/complete for
        // authority transmission. Even zero UI errors MUST remain fail-closed.
        Add("invoice.rules.not_approved","validation",
            "پیش‌بررسی داخلی انجام شد؛ اما شناسه مالیاتی صورتحساب، نسخه معتبر قواعد ارسال و بسته امضاشده هنوز برای صدور مالیاتی تأیید نشده‌اند. تأیید داخلی با پذیرش سازمان متفاوت است.",true);
        return new(InvoiceApiDecision.Success,
            new(draft.Id,draft.Revision,OfficialV79InvoicePatternMatrix.Edition,
                false,findings.AsReadOnly()));
    }

    public async Task<InvoiceUiReferenceResult> ListReferencesAsync(
        string? token, Guid taxpayerId, CancellationToken ct = default)
    {
        var principal = await scope.ResolveAsync(token,ct);
        if (principal.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated);
        if (principal.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);
        var owner = await scope.ScopedQuery(principal.ActorId,principal.Level1)
            .Where(x=>x.Id==taxpayerId).Select(x=>x.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if (owner is null || owner == Guid.Empty)
            return new(InvoiceApiDecision.NotFound);

        var rows = await db.InvoiceDocuments.AsNoTracking()
            .Where(x=>x.TaxpayerId==taxpayerId && x.CustomerUserId==owner)
            .OrderByDescending(x=>x.IssueDateUtc).ThenBy(x=>x.Id)
            .Take(100).ToArrayAsync(ct);
        // Current persistence has no authoritative registered fiscal IDs.
        // Do not expose a selectable reference or mark any as eligible.
        var result = rows.Select(x=>new InvoiceUiReference(x.Id,
            x.Subject.ToString(),x.State.ToString(),x.IssueDateUtc,
            x.Revision,false,null)).ToArray();
        return new(InvoiceApiDecision.Success,result);
    }
}
