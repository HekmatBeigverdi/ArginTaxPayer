using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum Phase10DispatchAttemptStartStatus
{
    Started = 1,
    StaleOrWrongScope = 2,
    Invalid = 3
}

public sealed record Phase10DispatchAttemptStartResult(
    Phase10DispatchAttemptStartStatus Status,
    Guid? AttemptId = null,
    long? NewStateRevision = null,
    int? AttemptNumber = null);

public sealed record Phase10DispatchOperationState(
    Guid OperationId, Guid CustomerUserId, Guid TaxpayerId,
    InvoiceDispatchState State, long StateRevision);

/// <summary>
/// Claims one Reserved dispatch operation for one external send. The state CAS,
/// attempt append and attempt number allocation are one transaction. No HTTP
/// call is made here.
/// </summary>
public sealed class Phase10DispatchAttemptRepository(ArginTaxPayerDbContext db)
{
    internal async Task<Phase10DispatchOperationState?> ReadStateAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId,
        CancellationToken ct = default)
    {
        var row = await db.Database.SqlQuery<Phase10DispatchOperationState>($"""
SELECT id AS "OperationId", customer_user_id AS "CustomerUserId",
       taxpayer_id AS "TaxpayerId", state AS "State", state_revision AS "StateRevision"
FROM invoice_dispatch_operations
WHERE id={operationId} AND customer_user_id={customerUserId} AND taxpayer_id={taxpayerId}
""").SingleOrDefaultAsync(ct);
        return row;
    }

    public async Task<Phase10DispatchAttemptStartResult> BeginSendAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId,
        long expectedStateRevision, CancellationToken ct = default)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || expectedStateRevision < 1)
            return new(Phase10DispatchAttemptStartStatus.Invalid);

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var tx = await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            try
            {
                await using var database = Command(connection, tx,
                    "SELECT current_database()");
                if ((string?)await database.ExecuteScalarAsync(ct) != "argintaxpayer_test")
                    throw new InvalidOperationException("Phase10 dispatch requires isolated test DB.");

                var now = DateTime.UtcNow;
                await using var advance = Command(connection, tx, """
UPDATE invoice_dispatch_operations
SET state=@next,state_revision=state_revision+1,updated_at_utc=@at
WHERE id=@id AND customer_user_id=@customer AND taxpayer_id=@taxpayer
 AND state=@current AND state_revision=@revision
RETURNING state_revision
""", ("next", (int)InvoiceDispatchState.Sending), ("at", now),
                    ("id", operationId), ("customer", customerUserId),
                    ("taxpayer", taxpayerId), ("current", (int)InvoiceDispatchState.Reserved),
                    ("revision", expectedStateRevision));
                var nextRevision = await advance.ExecuteScalarAsync(ct);
                if (nextRevision is not long revision)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    return new(Phase10DispatchAttemptStartStatus.StaleOrWrongScope);
                }

                await using var numberCommand = Command(connection, tx, """
SELECT COALESCE(MAX(attempt_number),0)+1
FROM invoice_dispatch_attempts
WHERE operation_id=@operation
""", ("operation", operationId));
                var numberValue = await numberCommand.ExecuteScalarAsync(ct);
                if (numberValue is not int attemptNumber || attemptNumber < 1)
                    throw new InvalidOperationException("Invalid dispatch attempt number.");

                var attemptId = Guid.NewGuid();
                await using var insert = Command(connection, tx, """
INSERT INTO invoice_dispatch_attempts
(id,operation_id,attempt_number,endpoint_code,started_at_utc)
VALUES (@id,@operation,@number,'tax-authority-submit',@started)
""", ("id", attemptId), ("operation", operationId),
                    ("number", attemptNumber), ("started", now));
                await insert.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
                return new(Phase10DispatchAttemptStartStatus.Started,
                    attemptId, revision, attemptNumber);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    private static DbCommand Command(DbConnection connection, DbTransaction transaction,
        string sql, params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
