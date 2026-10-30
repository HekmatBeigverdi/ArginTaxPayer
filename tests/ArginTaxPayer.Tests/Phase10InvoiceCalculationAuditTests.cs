using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10InvoiceCalculationAuditTests
{
    private static readonly Guid InvoiceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UnitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime IssueDate = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private static string Snapshot(int? buyerPersonType = 2, decimal? w2 = 0,
        decimal? vatRate = 10m, decimal claimedVat = 18m,
        string? issueDate = "2026-10-25T12:00:00Z")
    {
        var line = new {
            CatalogItemId = Guid.NewGuid(), Description = "خدمات", Quantity = 2m,
            UnitPriceRial = 100m, DiscountPerUnitRial = 10m, VatRatePercent = vatRate,
            UnitId, OfficialIdentifierSnapshot = "1234567890123"
        };
        return JsonSerializer.Serialize(new {
            sourceWorkspaceId = InvoiceId, sourceRevision = 1, environment = "Sandbox",
            invoiceType = 1, pattern = 1, issueDateUtc = issueDate,
            buyerPersonType, lines = new[] { line },
            settlementMethod = 1, cashAmountRial = 198m, creditAmountRial = 0m,
            financialSummary = new {
                GrossRial = 200m, DiscountRial = 20m, AfterDiscountRial = 180m,
                PreviewVatRial = claimedVat, PreviewGrandTotalRial = 198m + (w2 ?? 0m)
            },
            otherTaxAndLegalFeesRial = w2
        });
    }

    [Fact]
    public void Audit_accepts_truncated_vat_and_explicit_zero_w2()
    {
        var result = Phase10InvoiceCalculationAuditService.AuditSnapshot(
            Snapshot(claimedVat: 18m), IssueDate);

        Assert.True(result.CanResend);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Audit_accepts_attached_catalog_rate_and_four_million_rial_line()
    {
        var line = new {
            CatalogItemId = Guid.NewGuid(), Description = "پودر قهوه نسکافه",
            Quantity = 1m, UnitPriceRial = 4_000_000m, DiscountPerUnitRial = 0m,
            VatRatePercent = 10m, UnitId,
            OfficialIdentifierSnapshot = "2906505201257"
        };
        var snapshot = JsonSerializer.Serialize(new {
            sourceWorkspaceId = InvoiceId, sourceRevision = 1, environment = "Sandbox",
            invoiceType = 1, pattern = 1, issueDateUtc = "2026-10-25T12:00:00Z",
            buyerPersonType = 2, lines = new[] { line },
            financialSummary = new {
                GrossRial = 4_000_000m, DiscountRial = 0m, AfterDiscountRial = 4_000_000m,
                PreviewVatRial = 400_000m, PreviewGrandTotalRial = 4_400_000m
            },
            otherTaxAndLegalFeesRial = 0m
        });

        var report = Phase10InvoiceCalculationAuditService.AuditSnapshot(snapshot, IssueDate);

        Assert.True(report.CanResend);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public void Audit_reports_all_organization_codes_for_invalid_snapshot()
    {
        var result = Phase10InvoiceCalculationAuditService.AuditSnapshot(
            Snapshot(buyerPersonType: null, w2: null, vatRate: null, claimedVat: 19m,
                issueDate: "not-a-date"), IssueDate);

        Assert.Contains(result.Findings, x => x.Code == "0204501");
        Assert.Contains(result.Findings, x => x.Code == "0304401");
        Assert.Contains(result.Findings, x => x.Code == "0200201");
        Assert.Contains(result.Findings, x => x.Code == "00010");
        Assert.Contains(result.Findings, x => x.Code == "00026");
        Assert.False(result.CanResend);
    }

    [Fact]
    public void Audit_rejects_non_utc_issue_date_even_when_text_is_parseable()
    {
        var result = Phase10InvoiceCalculationAuditService.AuditSnapshot(
            Snapshot(), new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Local));

        Assert.Contains(result.Findings, x => x.Code == "0200201");
    }

    [Fact]
    public void Audit_accepts_nonzero_w2_when_wire_totals_support_it()
    {
        var result = Phase10InvoiceCalculationAuditService.AuditSnapshot(
            Snapshot(w2: 1m), IssueDate);

        Assert.DoesNotContain(result.Findings, x => x.Code == "00026");
        Assert.True(result.CanResend);
    }

    [Fact]
    public void Audit_accepts_server_derived_buyer_type_and_legacy_zero_w2_defaults()
    {
        var result = Phase10InvoiceCalculationAuditService.AuditSnapshot(
            Snapshot(buyerPersonType: null, w2: null), IssueDate,
            buyerPersonTypeOverride: 2, otherTaxAndLegalFeesOverride: 0m);

        Assert.True(result.CanResend);
        Assert.Empty(result.Findings);
    }
}
