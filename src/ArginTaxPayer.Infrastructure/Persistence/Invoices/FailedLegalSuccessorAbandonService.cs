using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public sealed record FailedLegalSuccessorAbandonResult(InvoiceApiDecision Decision, string SafeCode);

/// <summary>Removes a terminally rejected legal successor from the effective invoice chain.</summary>
public sealed class FailedLegalSuccessorAbandonService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<FailedLegalSuccessorAbandonResult> AbandonAsync(string? bearer, Guid taxpayerId,
        Guid invoiceId, CancellationToken ct)
    {
        var actor = await scope.ResolveAsync(bearer, ct);
        if (actor.Decision != TaxpayerScopeDecision.Allowed)
            return new(actor.Decision == TaxpayerScopeDecision.Unauthenticated
                ? InvoiceApiDecision.Unauthenticated : InvoiceApiDecision.Forbidden, "access_denied");
        var owner = await scope.ScopedQuery(actor.ActorId, actor.Level1).Where(t => t.Id == taxpayerId)
            .Select(t => t.CustomerUserId).SingleOrDefaultAsync(ct);
        if (owner is null) return new(InvoiceApiDecision.NotFound, "not_found");
        var document = await db.InvoiceDocuments.AsNoTracking().Where(i => i.Id == invoiceId &&
                i.TaxpayerId == taxpayerId && i.CustomerUserId == owner.Value)
            .Select(i => new { i.Subject, i.SnapshotJson }).SingleOrDefaultAsync(ct);
        if (document is null) return new(InvoiceApiDecision.NotFound, "not_found");
        if (document.Subject is not (InvoiceSubject.Correction or InvoiceSubject.SalesReturn or InvoiceSubject.Cancellation))
            return new(InvoiceApiDecision.Conflict, "not_a_legal_successor");

        string environment;
        try
        {
            using var snapshot = JsonDocument.Parse(document.SnapshotJson);
            environment = snapshot.RootElement.TryGetProperty("environment", out var value) &&
                value.GetString() == "Production" ? "Production" : "Sandbox";
        }
        catch (JsonException) { return new(InvoiceApiDecision.Conflict, "invoice_snapshot_invalid"); }
        var relation = environment == "Production" ? "authority_invoice_legal_relations" : "sandbox_invoice_legal_relations";
        var reports = environment == "Production" ? "production_authority_inquiry_reports" : "sandbox_authority_inquiry_reports";
        var affected = await db.Database.ExecuteSqlRawAsync($"""
UPDATE {relation} rel SET state=3, evidence_reference='user_abandoned_failed_successor'
WHERE rel.customer_user_id=@owner AND rel.taxpayer_id=@taxpayer
 AND rel.successor_invoice_id=@invoice AND rel.subject=@subject AND rel.state IN (1,2,4)
 AND EXISTS (
   SELECT 1 FROM {reports} report
   WHERE report.invoice_id=rel.successor_invoice_id AND report.customer_user_id=rel.customer_user_id
    AND report.taxpayer_id=rel.taxpayer_id AND report.report_json->>'Status'='FAILED'
    AND report.observed_at_utc=(SELECT max(latest.observed_at_utc) FROM {reports} latest
      WHERE latest.invoice_id=report.invoice_id AND latest.customer_user_id=report.customer_user_id
       AND latest.taxpayer_id=report.taxpayer_id))
""", [new NpgsqlParameter("owner", owner.Value), new NpgsqlParameter("taxpayer", taxpayerId),
        new NpgsqlParameter("invoice", invoiceId), new NpgsqlParameter("subject", (int)document.Subject)], ct);
        if (affected == 1) return new(InvoiceApiDecision.Success, "failed_successor_abandoned");
        var alreadyAbandoned = await db.Database.SqlQueryRaw<int>($$"""
SELECT 1 AS "Value" FROM {{relation}} rel
WHERE rel.customer_user_id='{{owner.Value}}' AND rel.taxpayer_id='{{taxpayerId}}'
 AND rel.successor_invoice_id='{{invoiceId}}' AND rel.subject={{(int)document.Subject}} AND rel.state=3
 AND EXISTS (SELECT 1 FROM {{reports}} report WHERE report.invoice_id=rel.successor_invoice_id
  AND report.customer_user_id=rel.customer_user_id AND report.taxpayer_id=rel.taxpayer_id
  AND report.report_json->>'Status'='FAILED'
  AND report.observed_at_utc=(SELECT max(latest.observed_at_utc) FROM {{reports}} latest
   WHERE latest.invoice_id=report.invoice_id AND latest.customer_user_id=report.customer_user_id
    AND latest.taxpayer_id=report.taxpayer_id))
LIMIT 1
""").AnyAsync(ct);
        return alreadyAbandoned ? new(InvoiceApiDecision.Success, "failed_successor_already_abandoned")
            : new(InvoiceApiDecision.Conflict, "latest_authority_result_not_failed");
    }
}
