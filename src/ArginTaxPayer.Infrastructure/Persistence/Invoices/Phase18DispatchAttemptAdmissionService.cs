using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum Phase18DispatchAttemptDecision
{
    Started = 1, NotAuthorized = 2, StaleOrAlreadySent = 3,
    SubscriptionBlocked = 4
}

public sealed record Phase18DispatchAttemptResult(
    Phase18DispatchAttemptDecision Decision, Guid? AttemptId = null,
    long? StateRevision = null, string? BlockingCode = null);

/// <summary>
/// Durable, single-attempt-only claim for an existing, immutable legacy outbox
/// operation. Both Sandbox and Production are admitted by persisted
/// fiscal_memories + invoice_dispatch_operations environment agreement, not by
/// the database name or caller's HTTP environment. Does not perform HTTP IO.
/// </summary>
public sealed class Phase18DispatchAttemptAdmissionService(
    ArginTaxPayerDbContext db, Phase18AuthorityEndpointRoutingService routes)
{
    public async Task<Phase18DispatchAttemptResult> ClaimAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid invoiceId,
        long expectedRevision, TaxAuthorityEnvironment provider,
        CancellationToken ct = default)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || invoiceId == Guid.Empty ||
            expectedRevision <= 0 || provider is TaxAuthorityEnvironment.Unknown ||
            !Enum.IsDefined(provider))
            return new(Phase18DispatchAttemptDecision.NotAuthorized);

        // Read-only persisted scope and operator/provider gates precede the
        // transaction; the SQL below repeats every authorization predicate.
        if (await routes.ResolveAsync(operationId, customerUserId,
                taxpayerId, invoiceId, provider, "ENQUEUE", ct) is null)
            return new(Phase18DispatchAttemptDecision.NotAuthorized);

        var environmentValue = provider == TaxAuthorityEnvironment.Production ? 1 : 2;
        var environmentName = provider == TaxAuthorityEnvironment.Production
            ? "Production" : "Sandbox";
        var expectedEvent = provider == TaxAuthorityEnvironment.Production
            ? "invoice.production.legacy.dispatch.requested"
            : "invoice.sandbox.legacy.dispatch.requested";
        var expectedSchema = provider == TaxAuthorityEnvironment.Production
            ? "phase18.production.legacy.invoice.dispatch.v1"
            : "phase10.sandbox.legacy.invoice.dispatch.v1";

        var conn = db.Database.GetDbConnection();
        var opened = conn.State != ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);
        try
        {
            await using var tx = await conn.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            try
            {
                var now = DateTime.UtcNow;
                await using var claim = Cmd(conn, tx, """
UPDATE invoice_dispatch_operations d
SET state=4, state_revision=state_revision+1, updated_at_utc=@now
FROM fiscal_memories m, invoice_documents i, taxpayer_profiles t,
     outbox_messages o
WHERE d.id=@operation AND d.customer_user_id=@customer
  AND d.taxpayer_id=@taxpayer AND d.invoice_id=@invoice
  AND d.state=3 AND d.state_revision=@revision
  AND d.authority_environment=@environment
  AND m.id=d.fiscal_memory_id AND m.taxpayer_id=d.taxpayer_id
  AND m.authority_environment=@environment AND m.status=2
  AND t.id=d.taxpayer_id AND t.customer_user_id=d.customer_user_id
  AND i.id=d.invoice_id AND i.taxpayer_id=d.taxpayer_id
  AND i.customer_user_id=d.customer_user_id AND i.state=3
  AND i.revision=d.invoice_revision
  AND o.id=d.id AND o.taxpayer_id=d.taxpayer_id
  AND o.event_type=@event AND o.processed_at_utc IS NULL
  AND o.attempt_count=0
  AND o.payload_json::jsonb->>'schema'=@schema
  AND o.payload_json::jsonb->>'environment'=@environment_name
  AND o.payload_json::jsonb->>'operationId'=d.id::text
  AND o.payload_json::jsonb->>'invoiceId'=d.invoice_id::text
  AND o.payload_json::jsonb->>'taxpayerId'=d.taxpayer_id::text
  AND o.payload_json::jsonb->>'customerUserId'=d.customer_user_id::text
  AND o.payload_json::jsonb->>'fiscalMemoryId'=d.fiscal_memory_id::text
  AND o.payload_json::jsonb->>'requestSha256'=d.payload_sha256
  AND EXISTS(SELECT 1 FROM customer_taxpayer_memberships c
    WHERE c.customer_user_id=d.customer_user_id
      AND c.taxpayer_id=d.taxpayer_id AND c.unlinked_at_utc IS NULL)
  AND NOT EXISTS (SELECT 1 FROM invoice_dispatch_attempts a
    WHERE a.operation_id=d.id)
  AND NOT EXISTS (SELECT 1 FROM invoice_dispatch_receipts r
    WHERE r.operation_id=d.id)
RETURNING d.state_revision
""", ("operation", operationId), ("customer", customerUserId),
                    ("taxpayer", taxpayerId), ("invoice", invoiceId),
                    ("revision", expectedRevision),
                    ("environment", environmentValue),
                    ("environment_name", environmentName),
                    ("schema", expectedSchema), ("event", expectedEvent),
                    ("now", now));

                var next = await claim.ExecuteScalarAsync(ct);
                if (next is not long nextRevision)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    return new(Phase18DispatchAttemptDecision.StaleOrAlreadySent);
                }

                var subscriptionBlock = await InvoiceSubscriptionGate.ReserveAsync(
                    conn, tx, customerUserId, operationId, ct);
                if (subscriptionBlock is not null)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    return new(Phase18DispatchAttemptDecision.SubscriptionBlocked,
                        BlockingCode: subscriptionBlock);
                }

                var attemptId = Guid.NewGuid();
                await using var insert = Cmd(conn, tx, """
INSERT INTO invoice_dispatch_attempts
(id,operation_id,attempt_number,endpoint_code,started_at_utc)
VALUES (@id,@operation,1,'tax-authority-submit',@now)
""", ("id", attemptId), ("operation", operationId), ("now", now));
                await insert.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
                return new(Phase18DispatchAttemptDecision.Started,
                    attemptId, nextRevision);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally { if (opened) await conn.CloseAsync(); }
    }

    private static DbCommand Cmd(DbConnection db, DbTransaction tx,
        string sql, params (string Name, object Value)[] args)
    {
        var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name,value) in args)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
        }
        return cmd;
    }
}
