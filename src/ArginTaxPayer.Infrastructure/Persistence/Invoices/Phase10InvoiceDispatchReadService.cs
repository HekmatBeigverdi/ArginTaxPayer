using System.Data;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Phase10 Step16 authenticated, tenant-scoped read-only submission timeline.
/// Never returns provider body, payload, credentials or another tenant's state.
/// No HTTP authority I/O or invoice sending is implemented.
/// </summary>
public sealed record InvoiceDispatchOverview(
    Guid OperationId, Guid InvoiceId, long InvoiceRevision, string Status,
    long StateRevision, DateTime UpdatedAtUtc, string Environment,
    bool SubmissionEnabled, bool InquiryEnabled, bool FiscalAccepted,
    string? ReferenceNumber = null);

public sealed record InvoiceDispatchOverviewResult(
    InvoiceApiDecision Decision, InvoiceDispatchOverview? Overview = null, string Environment = "Unverified", bool LegacySandbox = false);

public sealed class Phase10InvoiceDispatchReadService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<InvoiceDispatchOverviewResult> GetAsync(
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
            .Where(x => x.Id == taxpayerId)
            .Select(x => x.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if (owner is null || owner.Value == Guid.Empty)
            return new(InvoiceApiDecision.NotFound);

        // Verify the requested invoice belongs to precisely the live tenant
        // and taxpayer, independently of a dispatch operation's stored scope.
        var exists = await db.InvoiceDocuments.AsNoTracking()
            .AnyAsync(x => x.Id == invoiceId &&
                x.TaxpayerId == taxpayerId &&
                x.CustomerUserId == owner.Value, ct);
        if (!exists) return new(InvoiceApiDecision.NotFound);

        var binding = await new Phase18InvoiceMemoryBindingService(db, scope,
            new Phase18PersistedAuthorityEnvironmentResolver(db)).ResolveAsync(bearer, taxpayerId, invoiceId, ct);
        var environment = binding.Decision == InvoiceApiDecision.Success
            ? binding.Environment.ToString() : "Unverified";

        var conn = db.Database.GetDbConnection();
        var openedHere = conn.State != ConnectionState.Open;
        if (openedHere) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
SELECT d.id, d.invoice_revision, d.state, d.state_revision, d.updated_at_utc,
       (SELECT r.authority_reference FROM invoice_dispatch_receipts r
        WHERE r.operation_id=d.id AND r.receipt_kind=1 AND r.authority_reference IS NOT NULL
        ORDER BY r.observed_at_utc DESC LIMIT 1),
       d.fiscal_memory_id, NULLIF(to_jsonb(d)->>'authority_environment','')::integer
FROM invoice_dispatch_operations d
WHERE d.customer_user_id=@owner AND d.taxpayer_id=@taxpayer AND d.invoice_id=@invoice
ORDER BY d.invoice_revision DESC, d.created_at_utc DESC, d.id
LIMIT 1
""";
            foreach (var (name, value) in new (string, object)[] {
                ("owner",owner.Value),("taxpayer",taxpayerId),("invoice",invoiceId) })
            {
                var parameter=cmd.CreateParameter();
                parameter.ParameterName=name;
                parameter.Value=value;
                cmd.Parameters.Add(parameter);
            }
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return new(InvoiceApiDecision.Success, Environment: environment, LegacySandbox: binding.LegacySandbox);
            var storedEnvironment = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
            if (binding.Decision != InvoiceApiDecision.Success || reader.GetGuid(6) != binding.MemoryId ||
                (storedEnvironment is not null &&
                 Phase18PersistedAuthorityOperationGate.FromPersistedValue(storedEnvironment.Value) != binding.Environment) ||
                (storedEnvironment is null && !binding.LegacySandbox))
                environment = "Unverified";
            var state = reader.GetInt32(2);
            var stateName = Enum.IsDefined(typeof(ArginTaxPayer.Domain.Invoices.InvoiceDispatchState),state)
                ? ((ArginTaxPayer.Domain.Invoices.InvoiceDispatchState)state).ToString()
                : "Unrecognized";
            return new(InvoiceApiDecision.Success, new(
                reader.GetGuid(0),invoiceId,reader.GetInt64(1),stateName,reader.GetInt64(3),
                reader.GetDateTime(4),environment,false,false,false,
                reader.IsDBNull(5)?null:reader.GetString(5)), environment, binding.LegacySandbox);
        }
        finally
        {
            if (openedHere) await conn.CloseAsync();
        }
    }
}
