using System.Text.Json;
using System.Text.Json.Nodes;
using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Projects immutable invoice quantities after committed or unresolved sales returns.</summary>
internal static class InvoiceLegalQuantityAdjustments
{
    internal sealed record SalesReturnBasis(InvoiceEntryLine[] Lines, decimal[] AlreadyReturned)
    {
        public InvoiceEntryLine[] AvailableLines(bool reserveOne) => Lines.Select((line, index) =>
            line with { Quantity = Math.Max(0m, line.Quantity - AlreadyReturned[index] - (reserveOne ? 1m : 0m)) })
            .ToArray();
    }

    public static async Task<SalesReturnBasis> ReadSalesReturnBasisAsync(
        ArginTaxPayerDbContext db, Guid owner, Guid taxpayer, Guid predecessor,
        bool production, CancellationToken ct)
    {
        var relations = production ? "authority_invoice_legal_relations" : "sandbox_invoice_legal_relations";
        var reports = production ? "production_authority_inquiry_reports" : "sandbox_authority_inquiry_reports";
        var basisId = await db.Database.SqlQueryRaw<Guid>($"""
            WITH RECURSIVE ancestors AS (
              SELECT d.id,d.subject,0 AS depth,ARRAY[d.id] AS visited
              FROM invoice_documents d
              WHERE d.id=@predecessor AND d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer
              UNION ALL
              SELECT p.id,p.subject,a.depth+1,a.visited||p.id
              FROM ancestors a
              JOIN invoice_documents current ON current.id=a.id
                AND current.customer_user_id=@owner AND current.taxpayer_id=@taxpayer
              JOIN {relations} rel ON rel.successor_invoice_id=a.id
                AND rel.customer_user_id=@owner AND rel.taxpayer_id=@taxpayer
                AND rel.state IN (1,2,4)
                AND NOT EXISTS (SELECT 1 FROM {reports} report
                  WHERE report.invoice_id=rel.successor_invoice_id
                   AND report.customer_user_id=rel.customer_user_id AND report.taxpayer_id=rel.taxpayer_id
                   AND report.report_json->>'Status'='FAILED'
                   AND NOT EXISTS (SELECT 1 FROM {reports} newer
                     WHERE newer.invoice_id=report.invoice_id AND newer.customer_user_id=report.customer_user_id
                      AND newer.taxpayer_id=report.taxpayer_id
                      AND (newer.observed_at_utc,newer.id)>(report.observed_at_utc,report.id)))
              JOIN invoice_documents p ON p.id=rel.predecessor_invoice_id
                AND p.customer_user_id=@owner AND p.taxpayer_id=@taxpayer
              WHERE current.subject=4 AND a.depth<100 AND NOT p.id=ANY(a.visited)
            )
            SELECT id AS "Value" FROM ancestors
            WHERE subject<>4 ORDER BY depth LIMIT 1
            """, new NpgsqlParameter("predecessor", predecessor),
            new NpgsqlParameter("owner", owner), new NpgsqlParameter("taxpayer", taxpayer))
            .SingleOrDefaultAsync(ct);
        if (basisId == Guid.Empty)
            throw new InvalidOperationException("Sales return quantity basis is unavailable.");

        var basisJson = await db.InvoiceDocuments.AsNoTracking().Where(x =>
                x.Id == basisId && x.CustomerUserId == owner && x.TaxpayerId == taxpayer)
            .Select(x => x.SnapshotJson).SingleAsync(ct);
        using var basisSnapshot = JsonDocument.Parse(basisJson);
        var lines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            basisSnapshot.RootElement.GetProperty("lines").GetRawText()) ?? [];
        var used = new decimal[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var position = i + 1;
            used[i] = await db.Database.SqlQueryRaw<decimal>($"""
                WITH RECURSIVE chain AS (
                  SELECT @basis::uuid AS invoice_id,ARRAY[@basis::uuid] AS visited
                  UNION ALL
                  SELECT rel.successor_invoice_id,chain.visited||rel.successor_invoice_id
                  FROM chain JOIN {relations} rel
                    ON rel.predecessor_invoice_id=chain.invoice_id
                   AND rel.customer_user_id=@owner AND rel.taxpayer_id=@taxpayer
                   AND rel.state IN (1,2,4)
                   AND NOT EXISTS (SELECT 1 FROM {reports} report
                     WHERE report.invoice_id=rel.successor_invoice_id
                      AND report.customer_user_id=rel.customer_user_id AND report.taxpayer_id=rel.taxpayer_id
                      AND report.report_json->>'Status'='FAILED'
                      AND NOT EXISTS (SELECT 1 FROM {reports} newer
                        WHERE newer.invoice_id=report.invoice_id AND newer.customer_user_id=report.customer_user_id
                         AND newer.taxpayer_id=report.taxpayer_id
                         AND (newer.observed_at_utc,newer.id)>(report.observed_at_utc,report.id)))
                  WHERE NOT rel.successor_invoice_id=ANY(chain.visited)
                )
                SELECT COALESCE(SUM((COALESCE(items.value->>'Quantity',items.value->>'quantity'))::numeric),0) AS "Value"
                FROM chain
                JOIN {relations} rel ON rel.successor_invoice_id=chain.invoice_id
                  AND rel.customer_user_id=@owner AND rel.taxpayer_id=@taxpayer
                  AND rel.subject=4 AND rel.state IN (1,2,4)
                JOIN invoice_documents successor ON successor.id=rel.successor_invoice_id
                  AND successor.customer_user_id=rel.customer_user_id AND successor.taxpayer_id=rel.taxpayer_id
                CROSS JOIN LATERAL jsonb_array_elements(successor.snapshot_json->'lines')
                  WITH ORDINALITY AS items(value,position)
                WHERE chain.invoice_id<>@basis AND items.position=@position
                """, new NpgsqlParameter("basis", basisId),
                new NpgsqlParameter("owner", owner), new NpgsqlParameter("taxpayer", taxpayer),
                new NpgsqlParameter("position", position)).SingleAsync(ct);
        }
        return new(lines, used);
    }

    public static Task<Guid?> FindLatestSuccessfulAsync(ArginTaxPayerDbContext db,
        Guid owner, Guid taxpayer, Guid root, bool production, CancellationToken ct)
    {
        var relations = production ? "authority_invoice_legal_relations" : "sandbox_invoice_legal_relations";
        var reports = production ? "production_authority_inquiry_reports" : "sandbox_authority_inquiry_reports";
        return db.Database.SqlQueryRaw<Guid?>($"""
            WITH RECURSIVE chain AS (
              SELECT d.id,d.issue_date_utc,0 AS depth,ARRAY[d.id] AS visited
              FROM invoice_documents d WHERE d.id=@root AND d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer
              UNION ALL
              SELECT d.id,d.issue_date_utc,c.depth+1,c.visited||d.id
              FROM chain c JOIN {relations} rel ON rel.predecessor_invoice_id=c.id
                AND rel.customer_user_id=@owner AND rel.taxpayer_id=@taxpayer AND rel.state IN (1,2,4)
                AND NOT EXISTS (SELECT 1 FROM {reports} failed
                  WHERE failed.invoice_id=rel.successor_invoice_id AND failed.customer_user_id=@owner
                    AND failed.taxpayer_id=@taxpayer AND failed.report_json->>'Status'='FAILED'
                    AND NOT EXISTS (SELECT 1 FROM {reports} newer
                      WHERE newer.invoice_id=failed.invoice_id AND newer.customer_user_id=failed.customer_user_id
                        AND newer.taxpayer_id=failed.taxpayer_id
                        AND (newer.observed_at_utc,newer.id)>(failed.observed_at_utc,failed.id)))
              JOIN invoice_documents d ON d.id=rel.successor_invoice_id
                AND d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer
              WHERE c.depth<100 AND NOT d.id=ANY(c.visited)
            )
            SELECT c.id AS "Value" FROM chain c
            WHERE EXISTS (SELECT 1 FROM {reports} report
              WHERE report.invoice_id=c.id AND report.customer_user_id=@owner
                AND report.taxpayer_id=@taxpayer AND report.report_json->>'Status'='SUCCESS'
                AND NOT EXISTS (SELECT 1 FROM {reports} newer
                  WHERE newer.invoice_id=report.invoice_id AND newer.customer_user_id=report.customer_user_id
                    AND newer.taxpayer_id=report.taxpayer_id
                    AND (newer.observed_at_utc,newer.id)>(report.observed_at_utc,report.id)))
            ORDER BY c.depth DESC,c.issue_date_utc DESC,c.id DESC LIMIT 1
            """, new NpgsqlParameter("root", root), new NpgsqlParameter("owner", owner),
            new NpgsqlParameter("taxpayer", taxpayer)).SingleOrDefaultAsync(ct);
    }

    public static async Task<string> CorrectionDraftWithBaselineAsync(
        ArginTaxPayerDbContext db, Guid owner, Guid taxpayer, Guid predecessor,
        string draftJson, bool production, CancellationToken ct)
    {
        var sourceJson = await db.InvoiceDocuments.AsNoTracking()
            .Where(x => x.Id == predecessor && x.CustomerUserId == owner && x.TaxpayerId == taxpayer)
            .Select(x => x.SnapshotJson).SingleAsync(ct);
        using var source = JsonDocument.Parse(sourceJson);
        var lines = JsonSerializer.Deserialize<InvoiceEntryLine[]>(
            source.RootElement.GetProperty("lines").GetRawText()) ?? [];
        var remaining = await AfterReturnsAsync(db, owner, taxpayer, predecessor,
            lines, production, reserveOne: false, ct);
        var draft = JsonNode.Parse(draftJson)!.AsObject();
        draft["correctionBaselineQuantities"] = JsonSerializer.SerializeToNode(remaining.Select(x => x.Quantity));
        return draft.ToJsonString();
    }

    public static async Task<InvoiceEntryLine[]> AfterReturnsAsync(
        ArginTaxPayerDbContext db, Guid owner, Guid taxpayer, Guid predecessor,
        IReadOnlyList<InvoiceEntryLine> lines, bool production, bool reserveOne,
        CancellationToken ct)
    {
        var relations = production ? "authority_invoice_legal_relations" : "sandbox_invoice_legal_relations";
        const string invoices = "invoice_documents";
        var result = lines.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            var position = i + 1;
            var sql = $"""
                SELECT COALESCE(SUM((COALESCE(items.value->>'Quantity',items.value->>'quantity'))::numeric),0) AS "Value"
                FROM {relations} rel
                JOIN {invoices} successor ON successor.id=rel.successor_invoice_id
                    AND successor.taxpayer_id=rel.taxpayer_id AND successor.customer_user_id=rel.customer_user_id
                CROSS JOIN LATERAL jsonb_array_elements(successor.snapshot_json::jsonb->'lines')
                    WITH ORDINALITY AS items(value,position)
                WHERE rel.customer_user_id=@owner AND rel.taxpayer_id=@taxpayer
                    AND rel.predecessor_invoice_id=@predecessor AND rel.subject=4
                    AND rel.state IN (1,2,4) AND items.position=@position
                """;
            var returned = await db.Database.SqlQueryRaw<decimal>(sql,
                new NpgsqlParameter("owner", owner), new NpgsqlParameter("taxpayer", taxpayer),
                new NpgsqlParameter("predecessor", predecessor), new NpgsqlParameter("position", position))
                .SingleAsync(ct);
            var available = result[i].Quantity - returned - (reserveOne ? 1m : 0m);
            result[i] = result[i] with { Quantity = Math.Max(0m, available) };
        }
        return result;
    }
}
