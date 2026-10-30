using System.Data;
using System.Data.Common;
using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum DispatchFaultPersistenceStatus
{
    Recorded = 1,
    StaleOrWrongScope = 2,
    NotApplicable = 3
}

public sealed record DispatchFaultPersistenceResult(
    DispatchFaultPersistenceStatus Status, InvoiceDispatchState? NewState = null,
    long? NewRevision = null);

/// <summary>
/// Atomically persists only the two Step12 fault transitions, using an exact
/// scoped operation identity + state revision CAS. It never sends or retries.
/// Attempts/receipts are append-only and cannot be changed by this repository.
/// </summary>
public sealed class InvoiceDispatchFaultRepository(ArginTaxPayerDbContext db)
{
    public async Task<DispatchFaultPersistenceResult> RecordAsync(
        Guid operationId, Guid customerUserId, Guid taxpayerId,
        InvoiceDispatchState expectedState, long expectedStateRevision,
        InvoiceTransportFailureKind failure,
        CancellationToken ct = default)
    {
        if (operationId == Guid.Empty || customerUserId == Guid.Empty ||
            taxpayerId == Guid.Empty || expectedStateRevision < 1)
            throw new ArgumentException("Invalid scoped operation/revision.");

        var decision = Phase10RetrySafetyPolicy.Classify(expectedState, failure);
        if (decision.ProposedEvent is not { } ev ||
            decision.ProposedState is not { } target ||
            !Phase10RetrySafetyPolicy.TryPlanPersistedTransition(
                expectedState, ev, expectedStateRevision, out var verified) ||
            verified != target)
            return new(DispatchFaultPersistenceStatus.NotApplicable);

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                await using var identityCommand = connection.CreateCommand();
                identityCommand.Transaction = tx;
                identityCommand.CommandText = "SELECT current_database()";
                var dbName = (string?)await identityCommand.ExecuteScalarAsync(ct);
                if (dbName != "argintaxpayer_test")
                    throw new InvalidOperationException("Step12 writes require isolated test DB.");

                const string sql = """
UPDATE invoice_dispatch_operations
SET state=@next, state_revision=state_revision+1, updated_at_utc=@at
WHERE id=@id AND customer_user_id=@customer AND taxpayer_id=@taxpayer
 AND state=@current AND state_revision=@revision
RETURNING state_revision
""";
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                Add(cmd, "next", (int)target);
                Add(cmd, "at", DateTime.UtcNow);
                Add(cmd, "id", operationId);
                Add(cmd, "customer", customerUserId);
                Add(cmd, "taxpayer", taxpayerId);
                Add(cmd, "current", (int)expectedState);
                Add(cmd, "revision", expectedStateRevision);
                var updated = await cmd.ExecuteScalarAsync(ct);
                await tx.CommitAsync(ct);
                return updated is long revision
                    ? new(DispatchFaultPersistenceStatus.Recorded, target, revision)
                    : new(DispatchFaultPersistenceStatus.StaleOrWrongScope);
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

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
