using System.Globalization;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Api.Dashboard;

public sealed partial class Phase15DashboardReadService
{
    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web);

    public sealed record InvoiceReportItem(Guid Id, int Subject, DateTime IssueDateUtc,
        string? TaxId, long? Serial, string? Seller, string? Buyer, string Environment,
        string Status, string? TotalRial, string? VatRial);

    public sealed record InvoiceReportTotals(int Count, int SubmittedCount, int MissingVatCount,
        string TotalRial, string VatRial, int MissingTotalCount);

    public sealed record InvoiceReportGroup(int Subject, InvoiceReportTotals Totals);
    public sealed record InvoiceReportResult(Guid CustomerId, string CustomerName, Window Period,
        InvoiceReportTotals Totals, InvoiceReportGroup[] Groups, InvoiceReportItem[] Items);

    private sealed class ReportDocument
    {
        public Guid Id { get; set; }
        public Guid TaxpayerId { get; set; }
        public int Subject { get; set; }
        public DateTime IssueDateUtc { get; set; }
        public string Snapshot { get; set; } = "";
        public string? TaxId { get; set; }
        public long? Serial { get; set; }
        public string? Buyer { get; set; }
        public int? EnvironmentCode { get; set; }
        public bool Submitted { get; set; }
        public bool Attempted { get; set; }
        public string? AuthorityStatus { get; set; }
    }

    private sealed class ReportSchema
    {
        public bool SandboxReservations { get; set; }
        public bool AuthorityReservations { get; set; }
        public bool SandboxInquiries { get; set; }
        public bool ProductionInquiries { get; set; }
    }

    private sealed record FinancialFields(decimal? VatRial, decimal? PreviewVatRial,
        decimal? GrandTotalRial, decimal? PreviewGrandTotalRial);
    private sealed record SnapshotFields(long? BusinessInvoiceSerial, string? Seller,
        string? SellerDisplayName, string? Buyer, string? BuyerDisplayName,
        string? Environment, InvoiceEntryLine[]? Lines, decimal? OtherTaxAndLegalFeesRial,
        FinancialFields? FinancialSummary);
    private sealed record MeasuredInvoice(InvoiceReportItem Item, decimal? Total, decimal? Vat, bool Submitted);

    public async Task<InvoiceReportResult> InvoiceReportAsync(string bearer, Guid? customerId, Window window,
        int? subject, string? environment, CancellationToken ct)
    {
        if (subject is < 1 or > 4 || environment is not (null or "production" or "sandbox" or "local"))
            throw new ArgumentException("Unknown report filter.");
        var actor = await AuthorizeAsync(bearer, false, ct);
        if (actor.Role == "Customer" && customerId.HasValue && customerId != actor.Id)
            throw new UnauthorizedAccessException();
        var ownerId = actor.Role == "Customer" ? actor.Id : customerId.GetValueOrDefault();
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Select a customer.");
        if (actor.Role != "Customer" && !await db.IdentityMfaStates.AnyAsync(
                state => state.UserId == actor.Id && state.EnabledAtUtc != null, ct))
            throw new UnauthorizedAccessException();

        // Apply the same live customer and administrator scope as the invoice register.
        var customers = db.IdentityUsers.AsNoTracking().Where(u => u.Id == ownerId && u.IsActive &&
            u.Roles.Any(r => r.Role == IdentityRoleNames.Customer) &&
            !u.Roles.Any(r => r.Role == IdentityRoleNames.Level1 || r.Role == IdentityRoleNames.Level2));
        if (actor.Role == "Level2")
            customers = customers.Where(u => db.CustomerCommercialOwners.Any(o =>
                o.CustomerUserId == u.Id && o.PartnerLevel2UserId == actor.Id));
        var customer = await customers.Select(u => new
        {
            u.NormalizedMobile,
            Profile = db.SoftwareUserProfiles.Where(p => p.UserId == u.Id)
                .Select(p => new { p.CompanyName, PersonName = p.FirstName + " " + p.LastName })
                .FirstOrDefault()
        }).SingleOrDefaultAsync(ct);
        if (customer is null)
            throw new KeyNotFoundException();
        var customerName = new[] { customer.Profile?.CompanyName, customer.Profile?.PersonName, customer.NormalizedMobile }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? ownerId.ToString();

        var schema = await db.Database.SqlQueryRaw<ReportSchema>("""
            SELECT to_regclass('public.sandbox_invoice_fiscal_reservations') IS NOT NULL AS "SandboxReservations",
                   to_regclass('public.authority_invoice_fiscal_reservations') IS NOT NULL AS "AuthorityReservations",
                   to_regclass('public.sandbox_authority_inquiry_reports') IS NOT NULL AS "SandboxInquiries",
                   to_regclass('public.production_authority_inquiry_reports') IS NOT NULL AS "ProductionInquiries"
            """).SingleAsync(ct);
        var documents = db.Database.SqlQueryRaw<ReportDocument>(ReportSql(schema),
            ownerId, window.FromUtc, window.ToUtcExclusive, subject ?? 0);
        var taxpayers = scope.ScopedQuery(actor.Id, actor.Level1).Where(t => t.CustomerUserId == ownerId);
        var rows = await (from document in documents
                          join taxpayer in taxpayers on document.TaxpayerId equals taxpayer.Id
                          orderby document.IssueDateUtc descending, document.Id descending
                          select new { Document = document, Seller = taxpayer.DisplayName }).ToArrayAsync(ct);
        var items = rows.Select(row => Measure(row.Document, row.Seller))
            .Where(row => environment is null || row.Item.Environment == environment).ToArray();
        var groups = Enum.GetValues<InvoiceSubject>().Where(s => subject is null || (int)s == subject)
            .Select(s => new InvoiceReportGroup((int)s, Sum(items.Where(i => i.Item.Subject == (int)s)))).ToArray();
        return new(ownerId, customerName, window, Sum(items), groups, items.Select(i => i.Item).ToArray());
    }

    private static string ReportSql(ReportSchema schema)
    {
        // Optional tables differ between sandbox and production deployments.
        // Only these fixed fragments are composed into SQL. Filters are parameters.
        var reservations = new List<string> { "SELECT NULL::text AS fiscal_tax_id, NULL::integer AS environment, NULL::timestamptz AS created_at_utc WHERE false" };
        if (schema.AuthorityReservations)
            reservations.Add(Reservation("authority_invoice_fiscal_reservations", "r.authority_environment"));
        if (schema.SandboxReservations)
            reservations.Add(Reservation("sandbox_invoice_fiscal_reservations", "2"));
        var inquiries = new List<string> { "SELECT NULL::text AS status, NULL::timestamptz AS observed_at_utc, NULL::uuid AS id WHERE false" };
        if (schema.ProductionInquiries)
            inquiries.Add(Inquiry("production_authority_inquiry_reports"));
        if (schema.SandboxInquiries)
            inquiries.Add(Inquiry("sandbox_authority_inquiry_reports"));

        return """
            SELECT d.id AS "Id", d.taxpayer_id AS "TaxpayerId", d.subject AS "Subject",
              d.issue_date_utc AS "IssueDateUtc", d.snapshot_json::text AS "Snapshot",
              reservation.fiscal_tax_id AS "TaxId", serial.serial AS "Serial", buyer.display_name AS "Buyer",
              COALESCE(reservation.environment, dispatch.environment) AS "EnvironmentCode",
              COALESCE(dispatch.submitted, false) OR inquiry.status IS NOT NULL AS "Submitted",
              COALESCE(dispatch.attempted, false) AS "Attempted", inquiry.status AS "AuthorityStatus"
            FROM invoice_documents d
            LEFT JOIN invoice_business_serials serial ON serial.workspace_id = d.id AND serial.taxpayer_id = d.taxpayer_id
            LEFT JOIN parties buyer ON buyer.id::text = COALESCE(d.snapshot_json->>'buyerPartyId', d.snapshot_json->>'BuyerPartyId')
              AND (buyer.owner_customer_user_id = d.customer_user_id OR EXISTS (
                SELECT 1 FROM customer_party_links link WHERE link.party_id = buyer.id AND link.customer_user_id = d.customer_user_id))
            LEFT JOIN LATERAL (
              SELECT fiscal_tax_id, environment FROM (REPORT_RESERVATIONS) r
              ORDER BY created_at_utc DESC, fiscal_tax_id DESC LIMIT 1
            ) reservation ON true
            LEFT JOIN LATERAL (
              SELECT NULLIF(to_jsonb(op)->>'authority_environment', '')::integer AS environment,
                EXISTS (SELECT 1 FROM invoice_dispatch_receipts rc WHERE rc.operation_id = op.id AND rc.receipt_kind = 1) AS submitted,
                EXISTS (SELECT 1 FROM invoice_dispatch_attempts a WHERE a.operation_id = op.id) AS attempted
              FROM invoice_dispatch_operations op
              WHERE op.invoice_id = d.id AND op.customer_user_id = d.customer_user_id AND op.taxpayer_id = d.taxpayer_id
              ORDER BY submitted DESC, attempted DESC, op.invoice_revision DESC, op.created_at_utc DESC, op.id DESC LIMIT 1
            ) dispatch ON true
            LEFT JOIN LATERAL (
              SELECT status FROM (REPORT_INQUIRIES) q ORDER BY observed_at_utc DESC, id DESC LIMIT 1
            ) inquiry ON true
            WHERE d.deleted_at_utc IS NULL AND d.customer_user_id = {0}
              AND d.issue_date_utc >= {1} AND d.issue_date_utc < {2}
              AND ({3} = 0 OR d.subject = {3})
              AND (d.state = 3 OR dispatch.submitted OR dispatch.attempted OR inquiry.status IS NOT NULL)
            """.Replace("REPORT_RESERVATIONS", string.Join(" UNION ALL ", reservations))
               .Replace("REPORT_INQUIRIES", string.Join(" UNION ALL ", inquiries));
    }

    private static string Reservation(string table, string environment) => $"""
        SELECT r.fiscal_tax_id, {environment} AS environment, r.created_at_utc FROM {table} r
        WHERE r.invoice_id = d.id AND r.customer_user_id = d.customer_user_id AND r.taxpayer_id = d.taxpayer_id
        """;

    private static string Inquiry(string table) => $"""
        SELECT r.report_json->>'Status' AS status, r.observed_at_utc, r.id FROM {table} r
        WHERE r.invoice_id = d.id AND r.customer_user_id = d.customer_user_id AND r.taxpayer_id = d.taxpayer_id
        """;

    private static MeasuredInvoice Measure(ReportDocument row, string seller)
    {
        SnapshotFields? snapshot = null;
        try { snapshot = JsonSerializer.Deserialize<SnapshotFields>(row.Snapshot, ReportJson); }
        catch (JsonException) { /* Unreadable historical amounts remain unavailable. */ }
        var financial = snapshot?.FinancialSummary;
        var vat = financial?.VatRial ?? financial?.PreviewVatRial;
        var total = financial?.GrandTotalRial ?? financial?.PreviewGrandTotalRial;
        if ((vat is null || total is null) && snapshot?.Lines is { Length: > 0 } lines &&
            lines.All(line => line is not null))
        {
            try
            {
                var calculated = InvoiceDraftSettlementPolicy.Calculate(lines, snapshot.OtherTaxAndLegalFeesRial ?? 0);
                vat ??= calculated.PreviewVatRial;
                total ??= calculated.PreviewGrandTotalRial;
            }
            catch (Exception ex) when (ex is ArgumentException or OverflowException) { }
        }
        var environment = row.EnvironmentCode switch
        {
            1 => "production",
            2 => "sandbox",
            _ => snapshot?.Environment switch
            {
                "Production" => "production",
                "Sandbox" => "sandbox",
                _ => "local"
            }
        };
        var status = row.AuthorityStatus?.ToUpperInvariant() switch
        {
            "SUCCESS" => "accepted",
            "FAILED" or "REJECTED" => "rejected",
            _ when row.Submitted => "submitted",
            _ when row.Attempted => "attempted",
            _ => "registered"
        };
        var item = new InvoiceReportItem(row.Id, row.Subject, row.IssueDateUtc, row.TaxId,
            snapshot?.BusinessInvoiceSerial is > 0 ? snapshot.BusinessInvoiceSerial : row.Serial,
            snapshot?.Seller ?? snapshot?.SellerDisplayName ?? seller,
            snapshot?.Buyer ?? snapshot?.BuyerDisplayName ?? row.Buyer,
            environment, status, Money(total), Money(vat));
        return new(item, total, vat, row.Submitted);
    }

    private static InvoiceReportTotals Sum(IEnumerable<MeasuredInvoice> items)
    {
        int count = 0, submitted = 0, missingVat = 0, missingTotal = 0;
        decimal total = 0, vat = 0;
        foreach (var item in items)
        {
            count++;
            if (item.Submitted) submitted++;
            if (item.Vat is { } itemVat) vat += itemVat;
            else missingVat++;
            if (item.Total is { } itemTotal) total += itemTotal;
            else missingTotal++;
        }
        return new(count, submitted, missingVat, Money(total)!, Money(vat)!, missingTotal);
    }

    private static string? Money(decimal? amount) => amount?.ToString("0.############################", CultureInfo.InvariantCulture);
}
