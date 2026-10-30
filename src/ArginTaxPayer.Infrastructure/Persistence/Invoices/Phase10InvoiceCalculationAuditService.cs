using System.Globalization;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record InvoiceCalculationFinding(string Code, string Field,
    string Message, string? Expected = null, string? Actual = null);

public sealed record InvoiceCalculationAuditReport(long? InvoiceRevision,
    bool CanResend, IReadOnlyList<InvoiceCalculationFinding> Findings);

public sealed record InvoiceCalculationAuditResult(InvoiceApiDecision Decision,
    InvoiceCalculationAuditReport? Report = null);

/// <summary>
/// Server-owned audit of the immutable invoice snapshot. It is deliberately
/// read-only and contains no authority I/O or retry side effect.
/// </summary>
public sealed class Phase10InvoiceCalculationAuditService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<InvoiceCalculationAuditResult> AuditAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId,
        CancellationToken ct = default)
    {
        if (taxpayerId == Guid.Empty || invoiceId == Guid.Empty)
            return new(InvoiceApiDecision.Invalid);
        var actor = await scope.ResolveAsync(bearer, ct);
        if (actor.Decision == TaxpayerScopeDecision.Unauthenticated)
            return new(InvoiceApiDecision.Unauthenticated);
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
            return new(InvoiceApiDecision.Forbidden);
        var owner = await scope.ScopedQuery(actor.ActorId, actor.Level1)
            .Where(x => x.Id == taxpayerId).Select(x => x.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if (owner is null || owner == Guid.Empty)
            return new(InvoiceApiDecision.NotFound);
        var invoice = await db.InvoiceDocuments.AsNoTracking()
            .Where(x => x.Id == invoiceId && x.TaxpayerId == taxpayerId &&
                x.CustomerUserId == owner.Value)
            .Select(x => new { x.Revision, x.IssueDateUtc, x.SnapshotJson })
            .SingleOrDefaultAsync(ct);
        if (invoice is null)
            return new(InvoiceApiDecision.NotFound);
        int? buyerPersonTypeOverride = null;
        decimal? otherTaxAndLegalFeesOverride = null;
        try
        {
            using var document = JsonDocument.Parse(invoice.SnapshotJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("buyerPersonType", out _)
                && root.TryGetProperty("buyerPartyId", out var buyerField)
                && buyerField.TryGetGuid(out var buyerId))
                buyerPersonTypeOverride = await (from link in db.CustomerPartyLinks.AsNoTracking()
                    join party in db.Parties.AsNoTracking() on link.PartyId equals party.Id
                    where link.CustomerUserId == owner.Value && link.PartyId == buyerId &&
                        link.UnlinkedAtUtc == null && party.Status == PartyStatus.Active
                    select (int?)party.Kind).SingleOrDefaultAsync(ct);
            if (!root.TryGetProperty("otherTaxAndLegalFeesRial", out _))
                otherTaxAndLegalFeesOverride = 0m;
        }
        catch (JsonException) { }
        var report = AuditSnapshot(invoice.SnapshotJson, invoice.IssueDateUtc,
                buyerPersonTypeOverride, otherTaxAndLegalFeesOverride) with {
                InvoiceRevision = invoice.Revision
            };
        return new(InvoiceApiDecision.Success, report);
    }

    public static InvoiceCalculationAuditReport AuditSnapshot(
        string snapshotJson, DateTime issueDateUtc,
        int? buyerPersonTypeOverride = null,
        decimal? otherTaxAndLegalFeesOverride = null)
    {
        var findings = new List<InvoiceCalculationFinding>();
        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            var root = document.RootElement;
            AuditDate(root, issueDateUtc, findings);
            var buyerTypeValid = buyerPersonTypeOverride is >= 1 and <= 4;
            if (buyerPersonTypeOverride is null &&
                root.TryGetProperty("buyerPersonType", out var buyerType) &&
                buyerType.ValueKind == JsonValueKind.Number &&
                buyerType.TryGetInt32(out var buyerTypeValue))
                buyerTypeValid = buyerTypeValue is >= 1 and <= 4;
            if (!buyerTypeValid)
                Add(findings, "00010", "header.tob", "نوع شخص خریدار الزامی است.");

            var w2Valid = otherTaxAndLegalFeesOverride is >= 0 &&
                otherTaxAndLegalFeesOverride.Value == decimal.Truncate(otherTaxAndLegalFeesOverride.Value);
            if (otherTaxAndLegalFeesOverride is null &&
                root.TryGetProperty("otherTaxAndLegalFeesRial", out var w2) &&
                w2.ValueKind == JsonValueKind.Number && w2.TryGetDecimal(out var w2Value))
                w2Valid = w2Value >= 0 && w2Value == decimal.Truncate(w2Value);
            if (!w2Valid)
                Add(findings, "00026", "header.todam", "مجموع سایر مالیات، عوارض و وجوه قانونی باید صریحاً و به‌صورت عدد صحیح نامنفی ثبت شود.");

            if (!root.TryGetProperty("lines", out var linesField) ||
                linesField.ValueKind != JsonValueKind.Array)
            {
                Add(findings, "0204501", "body", "اقلام صورتحساب برای محاسبه مالیات معتبر نیستند.");
            }
            else
            {
                var lines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(linesField.GetRawText()) ?? [];
                for (var index = 0; index < lines.Length; index++)
                {
                    var rate = lines[index].VatRatePercent;
                    if (rate is null || rate < 0 || rate > 999.99m ||
                        decimal.Round(rate.Value, 2, MidpointRounding.ToZero) != rate.Value)
                        Add(findings, "0304401", $"body[{index}].vra",
                            "نرخ مالیات بر ارزش افزوده با مرجع سامانه منطبق نیست.", null,
                            rate?.ToString(CultureInfo.InvariantCulture));
                }

                try
                {
                    var calculationW2 = otherTaxAndLegalFeesOverride;
                    if (calculationW2 is null && root.TryGetProperty("otherTaxAndLegalFeesRial", out var w2Field) &&
                        w2Field.ValueKind == JsonValueKind.Number && w2Field.TryGetDecimal(out var parsedW2))
                        calculationW2 = parsedW2;
                    var inputs = lines.Select((line, index) => new InvoiceFinancialInput(
                        line.CatalogItemId ?? Guid.Empty, line.Quantity, line.UnitPriceRial,
                        decimal.Truncate(line.Quantity * line.DiscountPerUnitRial),
                        line.VatRatePercent, OtherTaxesRial: index == 0 ? calculationW2 ?? 0m : 0m)).ToArray();
                    var calculated = InvoiceFinancialCalculator.Preview(inputs,
                        new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
                        new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition));
                    var summary = root.TryGetProperty("financialSummary", out var summaryField)
                        ? summaryField : default;
                    var claimedVat = summary.ValueKind == JsonValueKind.Object &&
                        summary.TryGetProperty("PreviewVatRial", out var vatField) &&
                        vatField.TryGetDecimal(out var vatValue) ? vatValue : (decimal?)null;
                    var expectedVat = calculated.VatRial;
                    if (claimedVat != expectedVat)
                        Add(findings, "0204501", "body.vam",
                            "مبلغ مالیات بر ارزش افزوده با محاسبه رسمی منطبق نیست.",
                            expectedVat?.ToString(CultureInfo.InvariantCulture),
                            claimedVat?.ToString(CultureInfo.InvariantCulture));
                    CompareSummary(summary, "GrossRial", calculated.BeforeDiscountRial, findings);
                    CompareSummary(summary, "DiscountRial", calculated.DiscountRial, findings);
                    CompareSummary(summary, "AfterDiscountRial", calculated.AfterDiscountRial, findings);
                    CompareSummary(summary, "PreviewGrandTotalRial", calculated.GrandTotalRial, findings);
                }
                catch (Exception exception) when (exception is ArgumentException or OverflowException)
                {
                    Add(findings, "0204501", "body.vam",
                        "مبلغ مالیات بر ارزش افزوده از لحاظ محاسباتی معتبر نیست.");
                }
            }
        }
        catch (JsonException)
        {
            Add(findings, "0204501", "body", "ساختار مالی صورتحساب قابل محاسبه نیست.");
        }
        return new(null, findings.Count == 0, findings.AsReadOnly());
    }

    private static void AuditDate(JsonElement root, DateTime issueDateUtc,
        List<InvoiceCalculationFinding> findings)
    {
        if (issueDateUtc.Kind != DateTimeKind.Utc || issueDateUtc > DateTime.UtcNow ||
            !root.TryGetProperty("issueDateUtc", out var field) ||
            field.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(field.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed) ||
            parsed.UtcDateTime != issueDateUtc)
            Add(findings, "0200201", "header.indatim",
                "تاریخ و زمان صدور صورتحساب میلادی معتبر نیست.");
    }

    private static void Add(List<InvoiceCalculationFinding> findings, string code,
        string field, string message, string? expected = null, string? actual = null) =>
        findings.Add(new(code, field, message, expected, actual));

    private static void CompareSummary(JsonElement summary, string name, decimal? expected,
        List<InvoiceCalculationFinding> findings)
    {
        var actual = summary.ValueKind == JsonValueKind.Object &&
            summary.TryGetProperty(name, out var field) && field.TryGetDecimal(out var value)
            ? value : (decimal?)null;
        if (actual != expected)
            Add(findings, "0204501", $"financialSummary.{name}",
                "جمع‌های مالی صورتحساب با محاسبه رسمی منطبق نیست.",
                expected?.ToString(CultureInfo.InvariantCulture),
                actual?.ToString(CultureInfo.InvariantCulture));
    }
}
