using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum Phase18DispatchOutcomeDecision { Recorded=1, StaleOrWrongScope=2, Invalid=3 }

public sealed record Phase18DispatchOutcomeResult(
    Phase18DispatchOutcomeDecision Decision, string Status = "Blocked");

/// <summary>
/// Single durable outcome transition for an attempted classified dispatch.
/// A matched reference is an enqueue acknowledgement, NOT fiscal acceptance.
/// Unknown results remain Indeterminate and must be reconciled by inquiry,
/// never retransmitted. This repository performs no network IO.
/// </summary>
public sealed class Phase18DispatchOutcomeRepository(
    ArginTaxPayerDbContext db, Phase18PersistedAuthorityOperationGate gate)
{
    public async Task<Phase18DispatchOutcomeResult> RecordAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId, Guid invoiceId,
        Guid attemptId, long expectedStateRevision,
        TaxAuthorityEnvironment registeredProvider, bool acknowledged,
        string safeCode, string? referenceNumber, CancellationToken ct = default)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || invoiceId == Guid.Empty ||
            attemptId == Guid.Empty || expectedStateRevision < 1 ||
            !Enum.IsDefined(registeredProvider) ||
            registeredProvider == TaxAuthorityEnvironment.Unknown ||
            acknowledged != (referenceNumber is not null) ||
            safeCode is not { Length: > 0 and <= 100 } ||
            safeCode.Any(c => !(c is >= 'a' and <= 'z' || char.IsAsciiDigit(c) || c is '_' or '.')))
            return new(Phase18DispatchOutcomeDecision.Invalid);

        var binding = await gate.ResolveAsync(operationId, customerUserId,
            taxpayerId, invoiceId, ct);
        if (binding is not { } persisted || persisted.Environment != registeredProvider)
            return new(Phase18DispatchOutcomeDecision.StaleOrWrongScope);

        var environmentValue = registeredProvider == TaxAuthorityEnvironment.Production ? 1 : 2;
        var now = DateTime.UtcNow;
        string? hash = null;
        if (acknowledged)
        {
            try
            {
                hash = Phase10DispatchEvidenceRepository.EvidenceHash(
                    safeCode, referenceNumber, now);
            }
            catch (ArgumentException)
            {
                return new(Phase18DispatchOutcomeDecision.Invalid);
            }
        }

        var conn = db.Database.GetDbConnection();
        var opened = conn.State != ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);
        try
        {
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                // Lock the operation before inserting an append-only receipt,
                // so acknowledgement and indeterminate cannot both win.
                await using var validate = Cmd(conn, tx, """
SELECT d.state_revision FROM invoice_dispatch_operations d
JOIN fiscal_memories m ON m.id=d.fiscal_memory_id AND m.taxpayer_id=d.taxpayer_id
JOIN invoice_dispatch_attempts a ON a.operation_id=d.id
WHERE d.id=@operation AND d.customer_user_id=@customer
 AND d.taxpayer_id=@taxpayer AND d.invoice_id=@invoice
 AND d.authority_environment=@environment
 AND m.authority_environment=@environment AND m.status=2
 AND d.state=4 AND d.state_revision=@revision
 AND a.id=@attempt AND a.attempt_number=1
 AND NOT EXISTS(SELECT 1 FROM invoice_dispatch_receipts r WHERE r.operation_id=d.id)
FOR UPDATE OF d
""", ("operation", operationId), ("customer", customerUserId),
                    ("taxpayer", taxpayerId), ("invoice", invoiceId),
                    ("environment", environmentValue),
                    ("revision", expectedStateRevision), ("attempt", attemptId));
                if (await validate.ExecuteScalarAsync(ct) is not long)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    return new(Phase18DispatchOutcomeDecision.StaleOrWrongScope);
                }

                if (acknowledged)
                {
                    await using var receipt = Cmd(conn, tx, """
INSERT INTO invoice_dispatch_receipts
(id,operation_id,attempt_id,receipt_kind,authority_reference,
 evidence_sha256,observed_at_utc)
VALUES (@id,@operation,@attempt,1,@reference,@hash,@now)
""", ("id", Guid.NewGuid()), ("operation", operationId), ("attempt", attemptId),
                        ("reference", referenceNumber!), ("hash", hash!), ("now", now));
                    await receipt.ExecuteNonQueryAsync(ct);
                }
                await using var update = Cmd(conn, tx, """
UPDATE invoice_dispatch_operations
SET state=@state,state_revision=state_revision+1,updated_at_utc=@now
WHERE id=@operation AND customer_user_id=@customer
 AND taxpayer_id=@taxpayer AND state=4 AND state_revision=@revision
RETURNING id
""", ("state", acknowledged ? 5 : 6), ("now", now),
                    ("operation", operationId), ("customer", customerUserId),
                    ("taxpayer", taxpayerId), ("revision", expectedStateRevision));
                if (await update.ExecuteScalarAsync(ct) is not Guid)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    return new(Phase18DispatchOutcomeDecision.StaleOrWrongScope);
                }
                if (acknowledged)
                    await InvoiceSubscriptionGate.ConsumeAsync(conn, tx,
                        customerUserId, operationId, now, ct);

                await tx.CommitAsync(ct);
                return new(Phase18DispatchOutcomeDecision.Recorded,
                    acknowledged ? "Acknowledged" : "Indeterminate");
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally { if (opened) await conn.CloseAsync(); }
    }

    private static DbCommand Cmd(DbConnection conn, DbTransaction tx,
        string sql, params (string Name, object Value)[] parameters)
    {
        var cmd=conn.CreateCommand();
        cmd.Transaction=tx;
        cmd.CommandText=sql;
        foreach (var (name,value) in parameters)
        {
            var p=cmd.CreateParameter();
            p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);
        }
        return cmd;
    }
}
